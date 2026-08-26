# Test Coverage

Run this from Git Bash at the repository root:

```bash
./scripts/test-coverage.sh
```

Or, from inside the `scripts` directory:

```bash
bash ./test-coverage.sh
```

Install ReportGenerator once if needed:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

The script:

1. Runs the Catalog unit tests with Coverlet.
2. Generates a Cobertura coverage report.
3. Creates an HTML report at `coverage-report/index.html`.
4. Opens the report automatically on Windows.

Generated `TestResults/` and `coverage-report/` directories are ignored by Git.
