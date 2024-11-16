FROM node:alpine AS client
WORKDIR /app
COPY /client/package.json /client/package-lock.json ./
RUN npm ci
COPY /client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS server
WORKDIR /app
COPY server/LootGod.csproj ./
ARG RUNTIME=linux-musl-x64
RUN dotnet restore --runtime $RUNTIME
COPY server/*.cs server/entities/*.cs .
COPY --from=client /app/dist wwwroot
RUN dotnet publish -c Release -o out \
	--no-restore \
	--runtime $RUNTIME \
	--self-contained true \
	/p:PublishSingleFile=true

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine
WORKDIR /app
COPY --from=server /app/out .
COPY server/appsettings.json /app
EXPOSE 8080
USER app
ENV OTEL_SERVICE_NAME=LootGod
ENV OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
ENV OTEL_EXPORTER_OTLP_ENDPOINT="https://api.honeycomb.io"
HEALTHCHECK --interval=1s --timeout=1s --retries=1 \
	CMD wget -nv -t1 --spider 'http://localhost:8080/healthz' || exit 1
ENTRYPOINT ["./LootGod"]