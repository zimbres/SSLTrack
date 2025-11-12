FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/SSLTrack/SSLTrack.csproj", "src/SSLTrack/"]
RUN dotnet restore "./src/SSLTrack/SSLTrack.csproj"
COPY . .

ARG TARGETPLATFORM

RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
    RID=linux-arm ; \
    fi \
    && dotnet publish "src/SSLTrack/SSLTrack.csproj" -c Release -o /app/publish -r $RID --self-contained false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir data
COPY ["assets/data.db", "./data"]
RUN chown -R $APP_UID:$APP_UID /app/data
USER $APP_UID

ENTRYPOINT ["./SSLTrack"]