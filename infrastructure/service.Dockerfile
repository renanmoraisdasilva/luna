# Pinned by digest rather than by the floating `10.0` tag, matching the pattern already used for SQL Server
# in docker-compose.prod.yml. A tag can be repointed at different content at any time; a digest cannot, so the
# same commit always builds from the same base image. Dependabot tracks the Docker ecosystem, so a digest bump
# arrives as a reviewable pull request rather than silently changing what production runs.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS runtime
ARG PROJECT
ARG APP_UID=1654
ARG APP_GID=1654
WORKDIR /app
RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/* \
	&& if ! getent group app >/dev/null; then groupadd --system --gid "$APP_GID" app; fi \
	&& if ! getent passwd app >/dev/null; then useradd --system --uid "$APP_UID" --gid "$APP_GID" --create-home --home-dir /home/app --shell /usr/sbin/nologin app; fi \
	&& getent group app >/dev/null \
	&& getent passwd app >/dev/null \
	&& test "$(id -u app)" = "$APP_UID" \
	&& test "$(id -g app)" = "$APP_GID"
COPY --from=build /app/publish .
RUN APP_NAME="$(basename "$PROJECT" .csproj)" \
	&& printf '%s\n' "#!/bin/sh" "set -eu" "exec dotnet \"${APP_NAME}.dll\"" > /startup.sh \
	&& chmod +x /startup.sh \
	&& chown -R "$APP_UID:$APP_GID" /app /startup.sh
ENV HOME=/home/app
USER 1654:1654
EXPOSE 8080
ENTRYPOINT ["/startup.sh"]
