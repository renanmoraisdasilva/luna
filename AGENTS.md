# Luna Project Guide

## Documentation

- `documentation/roadmap.md` is the high-level project roadmap, phase status, and next priorities.
- `documentation/dev_phases/` contains design documents, specifications, architecture notes, and acceptance criteria for each development phase.
- Phase specifications describe what the system must do: business behavior, API capabilities, failure scenarios, and acceptance criteria.
- Phase architecture documents describe how the system is built: service boundaries, database ownership, backend layering, frontend architecture, authentication, persistence, testing, and architectural decisions.
- `documentation/ui_renderings/` contains the HTML UI references for the planned customer experience. Use these when implementing or reviewing frontend screens:
  - `1-front-page.html` - storefront
  - `2-details-page.html` - product details
  - `3-cart-page.html` - cart
  - `4-checkout-page.html` - checkout
  - `5-order-successful.html` - order confirmation
  - `6-my-order-page.html` - order history
  - `7-order-details-page.html` - order details
  - `8-account-page.html` - account
  - `9-log-in-page.html` - login
  - `10-register-page.html` - registration

## Repository areas

- `src/Services/` contains the backend services and their bounded contexts.
- `src/Frontend/` contains the Next.js customer application and gateway.
- `tests/Unit/`, `tests/Integration/`, and `tests/Smoke/` contain the centralized test suites.
- `infrastructure/` contains Docker Compose files, Dockerfiles, environment examples, and deployment scripts.
- `.github/workflows/` contains CI validation and Docker image publishing workflows.
- `scripts/` contains project helper scripts.

## Working with the documentation

Use the roadmap to understand what should be worked on next. Use the relevant phase specification to verify required behavior, and use the corresponding architecture document to verify that an implementation follows Luna's technical boundaries. UI renderings describe visual intent; the current frontend code and phase documents define the implemented behavior.
