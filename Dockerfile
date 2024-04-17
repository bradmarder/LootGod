FROM node:alpine AS client
WORKDIR /app
COPY /client/package.json /client/package-lock.json ./
RUN npm ci
COPY /client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS server
WORKDIR /app
COPY server/LootGod.csproj ./
ARG RUNTIME=linux-musl-x64
RUN dotnet restore --runtime $RUNTIME
COPY server/*.cs ./
RUN dotnet publish -c Release -o out \
	--no-restore \
	--runtime $RUNTIME \
	--self-contained true \
	/p:PublishSingleFile=true

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine
WORKDIR /app
COPY --from=server /app/out .
COPY --from=client /app/dist wwwroot
EXPOSE 8080
USER app
ENV DOTNET_EnableDiagnostics=0
HEALTHCHECK --interval=1s --timeout=1s --retries=1 \
	CMD wget -nv -t1 --spider 'http://localhost:8080/healthz' || exit 1
ENTRYPOINT ["./LootGod"]