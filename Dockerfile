FROM node:18.7.0-alpine AS node-build-env

# A directory within the virtualized Docker environment
# Becomes more relevant when using Docker Compose later
WORKDIR /app

# add `/app/node_modules/.bin` to $PATH
ENV PATH /app/node_modules/.bin:$PATH

# install app dependencies
COPY /client/package.json ./
COPY /client/package-lock.json ./
RUN npm install --silent
RUN npm install react-scripts -g --silent

# Copies everything over to Docker environment
COPY /client/ ./

# required ENV variable when compiling the bundle
ARG REACT_APP_API_PATH
ARG REACT_APP_TITLE

# compile the production bundle
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY server/*.csproj ./
RUN dotnet restore --runtime alpine-x64

# Copy everything else and build
COPY server/. ./
RUN dotnet publish -c Release -o out \
	--runtime alpine-x64 \
	--self-contained true \
	/p:PublishSingleFile=true

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=node-build-env /app/build wwwroot

# Uses port which is used by the actual application
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["./LootGod"]