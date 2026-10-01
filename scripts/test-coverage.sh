#!/usr/bin/env bash

set -uo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/.." && pwd)"
results_directory="$repository_root/TestResults"
coverage_directory="$repository_root/coverage-report"
summary_file="$coverage_directory/test-summary.md"
frontend_coverage_directory="$coverage_directory/frontend"
backend_coverage_directory="$coverage_directory/backend"
overall_status=0

rm -rf "$results_directory/Unit" "$results_directory/Integration" "$results_directory/Frontend" "$coverage_directory"
mkdir -p "$results_directory/Unit" "$results_directory/Integration" "$results_directory/Frontend" "$backend_coverage_directory"

cat > "$summary_file" <<EOF
# Luna Test and Coverage Summary

Generated: $(date '+%Y-%m-%d %H:%M:%S %Z')

| Suite | Status | Duration |
| --- | --- | --- |
EOF

run_suite() {
    local name="$1"
    local working_directory="$2"
    local log_file="$results_directory/${name// /-}.log"
    shift 2

    local start_time
    local end_time
    local duration
    local status
    local exit_code

    start_time=$(date +%s)
    echo "Running $name..."

    if (cd -- "$working_directory" && "$@") >"$log_file" 2>&1; then
        status="PASS"
        exit_code=0
    else
        status="FAIL"
        exit_code=$?
        overall_status=1
    fi

    end_time=$(date +%s)
    duration=$((end_time - start_time))
    printf '| %s | %s | %ss |\n' "$name" "$status" "$duration" >> "$summary_file"
    printf '\n## %s\n\nExit code: %s\n\n```text\n' "$name" "$exit_code" >> "$summary_file"
    tail -n 30 "$log_file" >> "$summary_file"
    printf '```\n' >> "$summary_file"
    echo "$name: $status (${duration}s)"
}

run_suite "Backend unit tests" "$repository_root" \
    dotnet test tests/Unit/Luna.UnitTests.csproj \
    --configuration Release \
    --nologo \
    --collect:"XPlat Code Coverage" \
    --settings "$repository_root/tests/coverage.runsettings" \
    --results-directory "$results_directory/Unit"

run_suite "Backend integration tests" "$repository_root" \
    dotnet test tests/Integration/Luna.IntegrationTests.csproj \
    --configuration Release \
    --nologo \
    --collect:"XPlat Code Coverage" \
    --settings "$repository_root/tests/coverage.runsettings" \
    --results-directory "$results_directory/Integration"

run_suite "Frontend lint" "$repository_root/src/Frontend" npm run lint

# `npm test` already passes --coverage, which is what makes Vitest enforce the 90% branch threshold.
run_suite "Frontend tests" "$repository_root/src/Frontend" \
    npm test -- \
    --coverage.reporter=json-summary \
    --coverage.reporter=html \
    --coverage.reporter=text \
    --coverage.reportsDirectory="$frontend_coverage_directory"

run_suite "Frontend build" "$repository_root/src/Frontend" npm run build

coverage_files=()
while IFS= read -r -d '' coverage_file; do
    coverage_files+=("$coverage_file")
done < <(find "$results_directory/Unit" "$results_directory/Integration" -type f -name 'coverage.cobertura.xml' -print0)

if [[ "${#coverage_files[@]}" -eq 0 ]]; then
    echo "No backend Cobertura coverage reports were generated." >&2
    overall_status=1
elif ! command -v reportgenerator >/dev/null 2>&1; then
    echo "ReportGenerator is required. Install it with: dotnet tool install -g dotnet-reportgenerator-globaltool" >&2
    overall_status=1
else
    report_paths=()
    for coverage_file in "${coverage_files[@]}"; do
        if command -v cygpath >/dev/null 2>&1; then
            report_paths+=("$(cygpath -w "$coverage_file")")
        else
            report_paths+=("$coverage_file")
        fi
    done
    reports="$(IFS=';'; echo "${report_paths[*]}")"
    if command -v cygpath >/dev/null 2>&1; then
        backend_target_directory="$(cygpath -w "$backend_coverage_directory")"
    else
        backend_target_directory="$backend_coverage_directory"
    fi
    reportgenerator \
        "-reports:$reports" \
        "-targetdir:$backend_target_directory" \
        '-reporttypes:Html;Cobertura' >"$results_directory/ReportGenerator.log" 2>&1 || overall_status=1

    # The backend gate lives in scripts/check-backend-coverage.mjs so that CI and this script decide it
    # identically. The frontend threshold is enforced by Vitest itself, which only applies it when a
    # coverage reporter is active — that is why `npm test` runs `vitest run --coverage`.
    if [[ -f "$backend_coverage_directory/Cobertura.xml" ]]; then
        if ! node "$repository_root/scripts/check-backend-coverage.mjs" \
            "$backend_coverage_directory/Cobertura.xml"; then
            overall_status=1
        fi
    else
        echo "The merged backend coverage report is missing; unable to enforce the backend branch coverage gate." >&2
        overall_status=1
    fi

    if [[ ! -f "$frontend_coverage_directory/coverage-summary.json" ]]; then
        echo "The frontend coverage summary is missing; unable to confirm the frontend branch coverage gate." >&2
        overall_status=1
    fi
fi

cat >> "$summary_file" <<EOF

## Coverage reports

- Backend coverage: [backend/index.html](backend/index.html)
- Frontend coverage: [frontend/index.html](frontend/index.html)

## Overall result

EOF

if [[ "$overall_status" -eq 0 ]]; then
    echo 'All test, lint, build, and coverage steps passed.' >> "$summary_file"
else
    echo 'One or more test, lint, build, or coverage steps failed.' >> "$summary_file"
fi

cat > "$coverage_directory/index.html" <<EOF
<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>Luna test and coverage report</title></head>
<body>
<h1>Luna test and coverage report</h1>
<p><a href="test-summary.md">Execution summary</a></p>
<ul>
<li><a href="backend/index.html">Backend coverage</a></li>
<li><a href="frontend/index.html">Frontend coverage</a></li>
</ul>
</body>
</html>
EOF

echo
cat "$summary_file"
echo
echo "Report created at $coverage_directory/index.html"
if command -v cygpath >/dev/null 2>&1 && command -v explorer.exe >/dev/null 2>&1; then
    windows_report_index="$(cygpath -w "$coverage_directory/index.html")"
    explorer.exe "$windows_report_index" >/dev/null 2>&1 &
else
    echo "Open $coverage_directory/index.html in a browser."
fi

exit "$overall_status"
