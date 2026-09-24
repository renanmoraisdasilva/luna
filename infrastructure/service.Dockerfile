FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
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
