FROM node:alpine AS node-build-env
WORKDIR /app

# add `/app/node_modules/.bin` to $PATH
ENV PATH /app/node_modules/.bin:$PATH

# ensure that all frameworks and libraries are using the optimal settings for performance and security
ENV NODE_ENV production

# install app dependencies
# --force is hacky but react-scripts isn't maintained...
COPY /client/package.json ./
COPY /client/package-lock.json ./
RUN npm ci --force
RUN npm install react-scripts -g

# Copies everything over to Docker environment
COPY /client/ ./

# required ENV variable when compiling the bundle
ARG REACT_APP_TITLE

# compile the production bundle
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build-env
WORKDIR /app

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Copy csproj and restore as distinct layers
COPY server/*.csproj ./
RUN dotnet restore --runtime alpine-x64

# Copy everything else and build
COPY server/. ./
RUN dotnet publish -c Release -o out \
	--no-restore \
	--runtime alpine-x64 \

	# https://andrewlock.net/should-i-use-self-contained-or-framework-dependent-publishing-in-docker-images/
	--self-contained true \

	# /p:PublishTrimmed=true \
	/p:PublishSingleFile=true

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=node-build-env /app/build wwwroot

# Uses port which is used by the actual application
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# For added security, you can opt out of the diagnostic pipeline. When you opt-out this allows the container to run as read-only.
ENV DOTNET_EnableDiagnostics=0

# Create/Run as user without root privileges
# RUN adduser --disabled-password \
#   --home /app \
#   --gecos '' dotnetuser && chown -R dotnetuser /app
# USER dotnetuser

ENTRYPOINT ["./LootGod"]