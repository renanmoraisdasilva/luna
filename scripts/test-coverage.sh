#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/.." && pwd)"
test_project="$repository_root/tests/Unit/Luna.UnitTests.csproj"
results_directory="$repository_root/TestResults/Unit"
report_directory="$repository_root/coverage-report"

rm -rf "$results_directory" "$report_directory"

echo "Running unit tests with coverage..."
dotnet test "$test_project" \
    --configuration Release \
    --nologo \
    --collect:"XPlat Code Coverage" \
    --settings "$repository_root/tests/coverage.runsettings" \
    --results-directory "$results_directory"

coverage_files=()
while IFS= read -r -d '' coverage_file; do
    coverage_files+=("$coverage_file")
done < <(find "$results_directory" -type f -name 'coverage.cobertura.xml' -print0)

if [[ "${#coverage_files[@]}" -eq 0 ]]; then
    echo "No Cobertura coverage report was generated under $results_directory." >&2
    exit 1
fi

if ! command -v reportgenerator >/dev/null 2>&1; then
    echo "ReportGenerator is required. Install it with: dotnet tool install -g dotnet-reportgenerator-globaltool" >&2
    exit 1
fi

reports="$(IFS=';'; echo "${coverage_files[*]}")"
echo "Generating HTML coverage report..."
reportgenerator \
    "-reports:$reports" \
    "-targetdir:$report_directory" \
    '-reporttypes:Html'

report_index="$report_directory/index.html"
if [[ ! -f "$report_index" ]]; then
    echo "HTML coverage report was not created at $report_index." >&2
    exit 1
fi

echo "Coverage report created at $report_index"
if command -v cygpath >/dev/null 2>&1 && command -v explorer.exe >/dev/null 2>&1; then
    windows_report_index="$(cygpath -w "$report_index")"
    explorer.exe "$windows_report_index" >/dev/null 2>&1 &
else
    echo "Open $report_index in a browser."
fi