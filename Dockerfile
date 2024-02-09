FROM node:alpine AS client
WORKDIR /app
COPY /client/package.json /client/package-lock.json ./
RUN npm ci
COPY /client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS api
WORKDIR /app
COPY server/LootGod.csproj ./
ARG RUNTIME=linux-musl-x64
RUN dotnet restore --runtime $RUNTIME
COPY server/. ./
RUN dotnet publish -c Release -o out \
	--no-restore \
	--runtime $RUNTIME \
	--self-contained true \
	/p:PublishSingleFile=true

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine
WORKDIR /app
COPY --from=api /app/out .
COPY --from=client /app/dist wwwroot
EXPOSE 8080

# Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 8: 'attempt to write a readonly database'.
#USER app
#RUN chmod -R 600 /mnt

ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["./LootGod"]