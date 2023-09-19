# Sunkenland Mod Template

Welcome to the Sunkenland Mod Template by Azumatt! This repository provides a .NET template for quickly scaffolding mods
for Sunkenland.

## Prerequisites

- .NET SDK (version you're targeting, e.g., .NET 5.0 or later)
- Git
- JetBrains Rider or Visual Studio (optional for GUI-based installation)

## Features
- This mod template automatically packages your mod into a .zip file when in Release mode for Thunderstore as well as Nexus.
- The contents of the zip file come from the Thunderstore folder. Make sure you're updating the `manifest.json` with your relevant information for Thunderstore.
- The README.md at the project root is auto copied to the Thunderstore folder, to keep both the same and auto-packaging a breeze.
- The zip files will auto append the version string when you change the version in the `Plugin.cs` file and places it inside of the project's Thunderstore/Nexus folders (respectively).
- A copy of the compiled mods will be placed in your specified output folders from the `environment.props` file.
- It also includes a .gitignore file for use with Git.


## Installation

### Using Command Line

1. Clone this repository:
   ```bash
   git clone https://github.com/AzumattDev/NewPrefabCloneTest.git
   ```

2. Navigate to the directory:
   ```bash
   cd NewPrefabCloneTest
   ```

3. Install the template:
   ```bash
   dotnet new install ./
   ```

This will install the Sunkenland Mod Template locally and make it available as a dotnet template.

### Using JetBrains Rider

1. Open JetBrains Rider.
2. Go to `File` > `New Solution`.
3. On the left, click `More templates`.
4. Click on the button in the middle that says "Install Template".
   ![](https://i.imgur.com/pLzWEzl.png)
5. Navigate to the directory where you cloned the `NewPrefabCloneTest` repository and select the root folder. Usually in the path of `C:\Users\YourName\RiderProjects\NewPrefabCloneTest`
   folder.
6. The template should now be available in the list.
7. Now, when you create a new project, you can select the template from the list on the left after stating you want to create a new solution. Make sure that you select the checkbox for `Put solution and project in the same directory` for the template to work properly. 

![](https://i.imgur.com/yVcYC4K.png)


## Usage

### Using Command Line

Once installed, create a new project with the template:

```bash
dotnet new slmt -n YourModName --AuthorName "Your Name"
```

Replace `YourModName` with the desired name for your mod project and "Your Name" with your desired author name. The
author name will default to "Azumatt" if not specified.

## Parameters

- `-n` or `--name`: Name of the output directory and the project.
- `--AuthorName`: The name of the mod's author. Default is "Azumatt".

## Feedback and Contributions

Feedback and contributions are welcome! If you find a bug or wish to request a feature, please open an issue.

For contributions:

1. Fork the repository.
2. Create a new branch with your changes.
3. Submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE.txt).