FROM node:18.7.0-alpine AS build-env

# A directory within the virtualized Docker environment
# Becomes more relevant when using Docker Compose later
WORKDIR /app

# add `/app/node_modules/.bin` to $PATH
ENV PATH /app/node_modules/.bin:$PATH

# install app dependencies
COPY package.json ./
COPY package-lock.json ./
RUN npm install --silent
RUN npm install react-scripts -g --silent

# Copies everything over to Docker environment
COPY . ./

# required ENV variable when compiling the bundle
ARG REACT_APP_API_PATH

# compile the production bundle
RUN npm run build

# deploy/serve static assets
FROM nginx:alpine
COPY --from=build-env /app/build /usr/share/nginx/html