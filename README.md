[![Nuget](https://img.shields.io/nuget/v/Ensync.SqlServer)](https://www.nuget.org/packages/Ensync.SqlServer/)

This is a library and command-line tool to merge C# entity classes to SQL Server tables -- code-first entity development without migrations.

This is a work in progress with a fair bit of missing functionality. I've been dog-fooding it myself on personal projects.

# Getting Started
1. Install the ensync tool (currently in alpha):
```
dotnet tool install --global Ensync.SqlServer --version 1.0.7-alpha
```
2. Navigate to a C# project directory in a command line window and type `ensync` with no arguments. If it's your first time running, a pair of config files will be created in the root of your project: `ensync.config.json` and `ensync.ignore.json`. (Take care that you run ensync in a project directory and not the solution directory. You'll get an error if you run in the solution directory because there's usually no buildable project in the solution directory.)
3. The config file indicates the source assembly that defines your data model along with one or more Sql Server database targets.

<details>
  <summary>Sample</summary>
  
  ```json
{
  "AssemblyPath": ".\\bin\\Debug\\net8.0\\LiteInvoice.Database.dll",
  "DatabaseTargets": [
    {
      "Name": "DefaultConnection",
      "Type": "SqlServer",
      "ConnectionString": "Server=(localdb)\\mssqllocaldb;Database=LiteInvoiceNet8;Integrated Security=true",
      "IsProduction": false
    }
  ]
}
```

</details>

4. As you add and modify your C# entity classes, periodically go to the console and type `ensync` to preview the SQL script of changes that will patch your database. If you're satisfied with the script, type `ensync --merge` to apply the changes.

# Why?
This is a reboot of my [ModelSync](https://github.com/adamfoneil/ModelSync) project, to break from the NETStandard2 dependency, refactor the diff engine with fresh eyes, and to rebuild all the tooling. The long term vision is to have a WPF app remake of [ModelSync UI](https://aosoftware.net/modelsync/).

I like code-first database development, but I never made peace with EF migrations. I find them way too fussy and complicated. They interefere with database development *flow* in my opinion. I'm looking for a more fluid and effortless experience around entity development. That's what this project is about.

A "fluid" experience does come with a trade-off, however. In a team environment, this library is not intended for merging to a local database. Local database merging works, but the generated SQL scripts are run and discarded, not saved as code in your repository.

For that reason, in team scenarios, it works best to merge to shared databases.
