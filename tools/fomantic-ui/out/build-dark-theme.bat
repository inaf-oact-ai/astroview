copy /Y "src\theme.config.dark" "src\theme.config"
CALL gulp build
copy /Y "dist\semantic.css" "..\..\..\src\AstroView.WebApp\wwwroot\semantic\semantic_dark.css"
