# Lessons learned

Durable engineering findings that are not descriptions of the current system. For how the system works
today, read [`architecture.md`](architecture.md). For phase status, read [`roadmap.md`](roadmap.md).

Each entry records what was assumed, what was measured, and what turned out to be true. Several entries
exist because a confident-sounding claim turned out to be wrong, and the only reason that surfaced was a
harness built to prove it.

---

## 1. Never ship a finding you have not measured

Two findings in a security review were rated critical. Both were wrong.

**"The client-credentials grant authenticates nothing."** Wrong. A harness built against the real token
endpoint showed OpenIddict 7.0 authenticates the client *before* the passthrough controller runs. The
setting that looked dangerous, `AcceptAnonymousClients()`, was not making the grant unauthenticated. What
it actually permitted was a password grant with no `client_id` at all. The hardening was kept, but the
reasoning was completely different from the one written down.

**"The payment handler forgets to save changes."** Wrong. `OrderStatus` has no `InTransit` member, so the
branch that would have mutated the aggregate never executes, and omitting the save is correct. A
characterisation test was added instead so the next reader does not have to re-derive it.

Both were caught only because the review demanded a reproduction, not because someone read carefully.
The token endpoint had no tests at all, which is why the mistake was invisible: **an area with no tests
is an area where any claim, however confident, is cheap to make and impossible to check.**

A third instruction turned out to be wrong in the opposite direction — a plan note asked for a `using`
to be removed from a middleware class, but that class genuinely catches `DbUpdateConcurrencyException`
and needs the import. A written plan is a hypothesis, not an authority.

### The practice that follows

- Reproduce before reporting. Build the smallest harness that makes the claim observable.
- When a review finds a bug in code with no tests, the real finding is the missing test.
- Prefer a characterisation test over a code change when current behaviour is correct but non-obvious.
- Say plainly when a finding is retracted. A quiet correction is indistinguishable from a fix.

---

## 2. Hardening for production can break the development stack

Requiring a signing key outside Development was correct and necessary. It also broke the Docker Compose
smoke check on `main`, and it was easy to misread the failure:

```
container infrastructure-identity-1 exited (139)
```

Every other service was healthy. Exit 139 is a segfault, which looks like a runtime bug and is not one.
The real cause, from the container log:

```
System.InvalidOperationException: OpenIddict:Keys:SigningKeyPath must be configured
when the environment is 'Production'.
```

CI boots the **local** compose file, which never set `ASPNETCORE_ENVIRONMENT`, so every container
defaulted to Production and the new hardening correctly refused to start.

The fix was one line in the local stack, plus an explicit DI scope. The boundary was verified rather than
assumed: `docker-compose.prod.yml` sets no environment, so production still boots as Production and still
requires the mounted key.

### The practice that follows

- A security control aimed at production will be triggered by the dev stack unless the dev stack states its
  environment explicitly.
- When a change breaks CI, read the container log before theorising. Exit codes are not diagnoses.
- Verify the boundary still holds after fixing the break. A fix that quietly weakens production is worse
  than the outage.

---

## 3. Security controls also apply to your own tests

A rate limiter was added to the token endpoint: 10 requests per minute, partitioned by client id, falling
back to the remote IP for unauthenticated requests. It worked exactly as designed, which meant it
throttled the integration suite.

```
Expected response.StatusCode to be HttpStatusCode.OK, but found HttpStatusCode.TooManyRequests
```

The grant tests share one host and exceed ten requests a minute. They failed *depending on execution
order*, which is the worst possible failure mode: green locally, red intermittently in CI, no obvious
cause.

The limiter is right. The tests needed to opt out, and `TokenEndpointRateLimitingTests` already builds its
own host at a low limit to verify the limiter itself.

The subtle part: the fix had to be an **environment variable**, not an in-memory setting in
`ConfigureAppConfiguration`. The limiter reads its permit limit while the Identity services are
registered, and that happens before `ConfigureAppConfiguration` sources are added. An in-memory value
arrives too late and the host silently falls back to the default. This is the same trap that makes the
signing key path require an environment variable.

### The practice that follows

- When adding a limit, a quota, or a throttle, decide immediately how the tests will not trip over it.
- Shared fixtures make ordering-dependent failures look like flakes. If a test passes alone and fails in
  the suite, suspect shared state, not the test.
- Configuration read during service registration must come from a source that exists at registration time.

---

## 4. Checkout is a saga with two permanent wedges

`CheckoutHandler` coordinates four services, each owning its own database. There is no transaction
spanning them, so the code compensates manually. It does this well for *expected* business failures:
insufficient inventory cancels the order, a declined card releases the reservation and marks the order
`PaymentFailed`, and a failed release gets its own error code so it is visible.

It did not handle the two *unexpected* ones. Between committing the order and reserving inventory there
was a window, and between authorizing payment and confirming the order there was another.

Wedge one, worked example. A customer with two items totalling R$340 clicks Pay:

1. The order row commits with status `Pending`.
2. The container is OOM-killed, or a deploy rolls the pod, or the machine loses power.
3. The browser shows a network error. The customer taps Pay again with the same `Idempotency-Key`.
4. `TryGetExistingCheckoutAsync` finds the order, sees it is not `Confirmed` with both references, and
   throws `CHECKOUT_IN_PROGRESS` — "This checkout attempt is already being processed."

Nothing is being processed. The customer is permanently unable to check out with that key, and the order
sits in the database as `Pending` forever.

Wedge two is worse, because the customer is out of pocket: Payments has authorized R$340, the confirming
write fails, the retry hits the same refusal, and there is a money hold with no order.

Both are **permanent**. A search across Orders and Inventory for `BackgroundService`, `IHostedService`,
`Hangfire`, `Quartz`, `reconcil`, `cleanup`, `sweep`, `Expiry` returns nothing. No process will ever come
and resolve a wedged order.

### The compounding problem

`InventoryReservationId` and `PaymentId` were written by a single `RecordCheckoutResult` call on the last
line of the saga. So a crash mid-way left stock held by Inventory with **no record in Orders that the
reservation existed**. There was no code that could connect those two facts, so the stock could not be
released. This is why retry logic alone cannot fix it: a retry cannot release a reservation it does not
know about.

### What was changed

Each external step is now persisted the moment it succeeds, so an interrupted checkout leaves a trail:

- `Order.RecordInventoryReservation` and `Order.RecordPaymentAuthorization` replace the combined
  `RecordCheckoutResult`. Each is guarded to `Pending`, and a payment cannot be recorded before a
  reservation, which is the ordering the domain already guaranteed.
- The handler saves after reserving, then saves after authorizing, then confirms and saves.
- Two tests prove it by failing a chosen save and asserting what reached the database. Both were verified
  to **fail** against the previous write-once-at-the-end behaviour.

### What is still missing

This makes recovery *possible*. It does not perform recovery.

- **A reconciler.** A background job that finds orders stuck in `Pending` past a threshold and unwinds
  them: release the reservation, void the payment authorization, mark the order failed. Nothing like this
  exists in any service.
- **Retry that resumes instead of refusing forever.** Once a reconciler exists, `TryGetExistingCheckoutAsync`
  can distinguish "genuinely in flight, seconds ago" from "wedged twenty minutes ago" and resume or fail
  the old attempt rather than throwing `CHECKOUT_IN_PROGRESS` at the customer indefinitely.

Until both exist, the trail is written but nobody reads it.

---

## 5. A vulnerability with no patch is a policy decision, not a code change

An audit failure appeared with seven high-severity findings that all traced to one package, `braces <= 3.0.3`,
a stack-exhaustion denial of service.

The obvious fix was wrong. `npm audit fix --force` proposed upgrading Tailwind to 4, which this repository
had deliberately rejected because v4 replaces the JavaScript config with a CSS-first `@theme` block and
fails *silently* — custom tokens stop resolving, classes compile, nothing renders.

Measuring it first showed the upgrade would not have worked anyway:

- `braces@3.0.3` is the newest version published. **There is no patch.**
- `@next/eslint-plugin-next@16.3.8` still hard-depends on `fast-glob@3.3.1`, so the ESLint config alone keeps
  `braces` in the tree. Tailwind 4 would not have removed it.

Everything affected was a devDependency, reached through build-time glob patterns that no user controls,
and the production dependency set was clean. So the gate now runs `npm audit --omit=dev`.

The justification is empirical rather than convenient: the three genuinely dangerous advisories caught
earlier that week — a Next.js RCE, `axios` SSRF and header injection, and a `dompurify` XSS — were **all
production dependencies**. Scoping the gate to production would have caught every one.

### The practice that follows

- Check whether a fix exists before planning a migration to obtain one.
- An audit gate that cannot go green trains people to ignore it. Scope it to what actually ships.
- Do not weaken a security gate silently. Write down what it no longer covers and why.

---

## 6. A public API surface can hide behind a dev-only dependency

`swagger-ui-react` must stay in `dependencies` even though it is only used to render the OpenAPI UI.
Moving it to `devDependencies` breaks `next build`, because the route is statically analysed at build time.
The code is dev-only in *behaviour* but not in *bundling*.

Separately, `OperationsSidebar.tsx` still links to `/swagger`. Since Swagger is now gated to Development and
to admins, that link is dead in production and has not been removed.

---

## 7. Windows tooling corrupts this repository

`Set-Content` and `WriteAllLines` write CRLF, which contradicts `.editorconfig` (`end_of_line = lf`) and
trips the format gate. They also strip the UTF-8 BOM, which 30 migration files carry. The fix is
`[IO.File]::WriteAllBytes` with explicit LF, or the editor tool.

A related trap: **`dotnet test` silently reused a stale test assembly** after a file was restored, and
reported a failure that contradicted the source on disk. When a test result disagrees with the code,
rebuild with `--no-incremental` before believing either.

---

## 8. Dependabot majors are migrations, and naming packages is not a policy

Dependabot surfaced 23 pull requests in one week, including several that would have broken the build.
Fourteen were closed with the reason recorded on the pull request rather than deleted, so the reasoning
survives.

Measuring the NuGet majors rather than guessing produced three distinct kinds of problem:

| Package | What actually happens |
| --- | --- |
| Respawn 7 | `CreateAsync` no longer accepts a connection string, it requires a `DbConnection`. Seven call sites break. |
| Serilog.AspNetCore 10 | Pulls `Microsoft.Extensions.*` to 10.0.0 and fails with `NU1605` downgrade errors against the net8.0 pins. Requires the .NET 10 migration. |
| Swashbuckle 10 | OpenAPI v2 moves `OpenApiDocument` out of `Microsoft.OpenApi.Models`; the Identity document filter stops compiling. |
| .NET 10 base images | `mcr.microsoft.com/dotnet/aspnet:10.0` contains only the 10.0 runtime. A `net8.0` application cannot start on it. |

The first attempt ignored majors by naming packages: `Microsoft.*` and `FluentAssertions`. That was not
enough. Serilog, Swashbuckle, Respawn, and `xunit.runner.visualstudio` all slipped through, because the
packages that get through are the ones nobody anticipated. The rule is now a wildcard on both the NuGet
and npm sides, with the specific rationale kept in comments. Minors and patches still arrive grouped.

One correction worth recording: **Tailwind 4 was closed as unsafe to auto-merge, and it would not have
fixed the `braces` advisory anyway.** Both statements were measured, not assumed.

---

## 9. Merging pull requests is not the same as testing them

`docker.yml` triggers on `workflow_run` for `main`. A pull request that edits `docker.yml` therefore
never runs `docker.yml`, and its green checks prove nothing about the change it makes. Two action bumps
showed 3/3 passing while exercising none of the code they touched.

They were verified by merging and watching the publish workflow succeed on `main`. That is a real check,
but it happens after the fact, and a failure would surface in the release pipeline rather than in review.

### The practice that follows

- Before trusting a green check, confirm the workflow it belongs to actually ran.
- Changes to release and publish pipelines need a path that validates them before merge.

---

## 10. Tooling notes worth keeping

- **GitHub CLI credential scopes.** Merging a pull request that edits `.github/workflows/` requires the
  `workflow` scope. A token without it fails with `refusing to allow an OAuth App to create or update
  workflow`. `gh auth refresh -h github.com -s workflow`. `git push` is unaffected, because it uses a
  different credential held by Git Credential Manager.
- **Credentials on this machine** live in three independent places: the Windows Credential Manager entry
  `gh:github.com:` for the CLI, `git:https://github.com` for git, and `%APPDATA%\GitHub Desktop` for the
  desktop app. Signing out of one does not affect the others.
- **Dependabot ignore rules take effect on the next run.** A run already in flight keeps using the config
  it loaded, so pull requests can arrive seconds after the rules land and still violate them.
- **Verify a stale claim.** One pull request titled only "bump react" was React **19**, a major, because
  the version was absent from the title. Reading the diff caught it; the title did not say so.
