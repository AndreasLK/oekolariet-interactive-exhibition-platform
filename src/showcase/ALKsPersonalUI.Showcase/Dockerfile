FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["ALKsPersonalUI.Showcase.csproj", "./"]
RUN dotnet restore "ALKsPersonalUI.Showcase.csproj" --no-cache
COPY . .
RUN dotnet publish "ALKsPersonalUI.Showcase.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM nginx:stable-alpine AS final
WORKDIR /usr/share/nginx/html
RUN rm -rf ./*
COPY --from=build /app/publish/wwwroot .
RUN printf 'server {\n\
    listen 80;\n\
    server_name _;\n\
    root /usr/share/nginx/html;\n\
    index index.html;\n\
    include /etc/nginx/mime.types;\n\
    location / {\n\
        try_files $uri $uri/ /index.html;\n\
    }\n\
}\n' > /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]