AstroView app is styled with Fomantic UI css framework. Default theme is light colored. To follow design mockups and make it dark we modify css sources and compile them into minified css file.

To customize Fomantic UI:
* Modify css sources in `\Tools\fomantic-ui\out\src`
* Open terminal in `\Tools\fomantic-ui` folder.
* Run `npm install`
* Go to "out" folder `cd out`
* Run `gulp build`
* Go to "dist" folder and copy `semantic.min.css` to `\AstroView.WebApp\wwwroot\semantic\` replacing existing file.
* Refresh website `Ctrl + F5`