FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SSLTrackAgent/SSLTrackAgent.csproj", "src/SSLTrackAgent/"]
RUN dotnet restore "./src/SSLTrackAgent/SSLTrackAgent.csproj"
COPY . .

ARG TARGETPLATFORM

RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
    RID=linux-arm ; \
    fi \
    && dotnet publish "src/SSLTrackAgent/SSLTrackAgent.csproj" -c Release -o /app/publish -r $RID --self-contained false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["./SSLTrackAgent"]