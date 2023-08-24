FROM node:alpine AS node-build-env
WORKDIR /app
COPY /client/package.json ./
COPY /client/package-lock.json ./
RUN npm ci
COPY /client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build-env
WORKDIR /app
COPY server/LootGod.csproj ./
ARG RUNTIME=alpine-x64
RUN dotnet restore --runtime $RUNTIME
COPY server/. ./
RUN dotnet publish -c Release -o out \
	--no-restore \
	--runtime $RUNTIME \
	--self-contained true \
	/p:PublishSingleFile=true

FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=node-build-env /app/dist wwwroot
ARG PORT=8080
EXPOSE $PORT
ENV ASPNETCORE_URLS=http://+:$PORT
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["./LootGod"]