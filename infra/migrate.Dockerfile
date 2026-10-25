# The migration image: the self-contained EF Core migration bundle on the smallest .NET base, run as
# the yello-migrate Container Apps Job before every Host revision (story 1.10, AR-36, AR-38).
#
# Built by the deploy workflow from the bundle it has just produced:
#   dotnet ef migrations bundle --project Yello.Infrastructure --startup-project Yello.Infrastructure \
#     --self-contained -r linux-x64 --configuration Release -o artifacts/efbundle/efbundle
#   docker build -f infra/migrate.Dockerfile -t <registry>/yello-migrate:<sha> artifacts/efbundle
#
# No secret is in the image. The connection string arrives at run time from Key Vault, as the
# ConnectionStrings__yello environment variable, and is passed to the bundle on its command line.
# The bundle applies only what __EFMigrationsHistory says is missing, so a run with nothing to apply
# exits 0.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.11
WORKDIR /app
COPY --chmod=0755 efbundle /app/efbundle
USER $APP_UID
ENTRYPOINT ["/bin/sh", "-c", "exec /app/efbundle --connection \"$ConnectionStrings__yello\""]
