copy /Y "src\theme.config.default" "src\theme.config"
CALL gulp build
copy /Y "src\theme.config.dark" "src\theme.config"
copy /Y "dist\semantic.css" "..\..\..\src\AstroView.WebApp\wwwroot\semantic\semantic.css"
