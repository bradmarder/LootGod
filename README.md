# Everquest Loot Distribution Tool

* Front-end built with Typescript/React/Parcel
* Back-end built with c#/EFCore/SQLite
* Uses SSE for real-time push notifications
* Structured logging and tracing with OTEL
* Source generator mapping with Mapperly
* Docker containers for both production deployment and dev lifecycle
* Build/Deploy with single command `flyctl deploy --local-only`
* Hosted on fly.io free tier
* Dev certs generated with `dotnet dev-certs https -ep ./localhost.pem -np --trust --format PEM`
* All runtime JSON deserialization disabled with `JsonSerializerIsReflectionEnabledByDefault` set to `false`
* Almost ready for `PublishAot` to be enabled (a few warnings remain due to EFCore)
* Absurdly overengineered