# Astroview

## Developer Setup

Technologies and Tools:
- .NET 9
- Python 3.7
- MySQL Server 8.4.x
- Visual Studio 2022
- (optional) Docker
- (optional) Node JS, Gulp

### Quick Start

Install MySQL Server locally or run it in Docker:

```
docker run --name astroview-mysql -p 3306:3306 -e MYSQL_ROOT_PASSWORD=Password_123 -d mysql:8.4.3
```

Update `appsettings.Development.json` file:
- Set proper database connection string
- In AppConfig section:
  - Check if you run Linux or not
  - Set path to the Storage and Library folders
  - Set path to Python executable. If you run Linux, look at the example in `appsettings.json`

Run project and use default admin credentials to login:
```
admin@admin
Password_123
```

### Setting up Python

PixPlot package requires Python 3.7. Install it manually or use an environment manager like Anaconda / Miniconda.

Install following packages:
```
pip install pixplot==0.0.112
pip install astropy==4.3.1
pip install scikit-image==0.19.3
```

**Example**: Setting up Python 3.7 with Anaconda. Open anaconda command prompt and run:
```
conda create --name 3.7 python=3.7 -y
conda activate 3.7
pip install pixplot==0.0.112
pip install astropy==4.3.1
pip install scikit-image==0.19.3
```

Set python path in AppConfig section in appsettings.Development.json file:
```
For Windows:
"PythonExecutable": "C:\\Users\\user1\\anaconda3\\envs\\3.7\\python.exe

For Linux:
"PythonExecutable": "~/anaconda3/bin/conda run --no-capture-output -n 3.7 python"
```

Now you should be able to generate PNG images and PixPlot maps in web application.

## Installation

### Running in Docker

#### Requirements
Docker Desktop: https://www.docker.com/products/docker-desktop/

#### Installing AstroView
On you disk create a folder for storing dataset files and note full path to it. 
In this example the path will be `D:\AstroView\Library`.

Create another folder that will be used for internal storage.
In this example the path will be `D:\AstroView\Storage`.

In repository folder open terminal and execute:
* `SET LIBRARY=D:\AstroView\Library` (for Windows)
* `SET STORAGE=D:\AstroView\Storage` (for Windows)
* `export LIBRARY="/AstroView/Library"` (for MAC)
* `export STORAGE="/AstroView/Storage"` (for MAC)
* `docker build -t astroview-webapp-base -f devops/astroview-webapp-base-dockerfile .` (this command may run for 20 minutes or more)
* `docker compose build`
* `docker compose up -d`

Website is up! Open http://localhost:5000/ and login with username `admin@admin` and password `Password_123`.

Next step: [Importing Sample Datasets](#importing-sample-datasets).

#### Updating AstroView to the latest version

* Pull the latest source codes from git repository.
* In Docker Desktop click on Containers menu item. There expand `astroview` stack and delete `webapp-1` container.
* Open terminal in repository folder and execute:
  * `SET LIBRARY=D:\AstroView\Library` (for Windows)
  * `SET STORAGE=D:\AstroView\Storage` (for Windows)
  * `export LIBRARY="/AstroView/Library"` (for MAC)
  * `export STORAGE="/AstroView/Storage"` (for MAC)
  * `docker compose build`
  * `docker compose up -d`

### Troubleshooting

##### Docker authorization problems in terminal

If you encounter Authorization problems with docker commands in terminal, try to relogin: `docker logout`, then `docker login --username XXX --password XXX`

##### Increase container RAM limit to 2-4 GB.

AstroView container requires more than 1 GB of RAM available otherwise docker service may become unresponsible. This behavior observed on Windows machines. 4 GB of RAM is recommended. 

In order to increase RAM limit in Windows you need to modify WSL config `C:\Users\user1\.wslconfig`, then shutdown wsl `wsl --shutdown` and start Docker service in Docker Desktop:

```
[wsl2]
memory=4GB # Limits VM memory in WSL 2 to 4 GB
processors=2 # Makes the WSL 2 VM use two virtual processors
```