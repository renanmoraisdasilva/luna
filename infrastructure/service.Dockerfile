FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ARG PROJECT
WORKDIR /app
RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
RUN APP_NAME="$(basename "$PROJECT" .csproj)" \
	&& printf '%s\n' "#!/bin/sh" "set -eu" "exec dotnet \"${APP_NAME}.dll\"" > /startup.sh \
	&& chmod +x /startup.sh
EXPOSE 8080
ENTRYPOINT ["/startup.sh"]