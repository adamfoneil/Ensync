This is a library and command-line tool to merge C# entity classes to SQL Server tables -- code-first entity development without migrations.

# Getting Started
1. Install the ensync tool:
```
dotnet tool install -g Ensync.SqlServer
```
2. Navigate to a C# project directory in a command line window and type `ensync` with no arguments.

# Why?
This is a reboot of my [ModelSync](https://github.com/adamfoneil/ModelSync) project, to break from the NETStandard2 dependency, refactor the diff engine with fresh eyes, and to rebuild all the tooling. The long term vision is to have a WPF app remake of [ModelSync UI](https://aosoftware.net/modelsync/).

I like code-first database development, but I never made peace with EF migrations. I find them way too fussy and complicated. They interefere with database development *flow* in my opinion. I'm looking for a more fluid and effortless experience around entity development. That's what this project is about.

A "fluid" experience does come with a trade-off, however. In a team environment, this library is not intended for merging to a local database. Local database merging works, but the generated SQL scripts are run and discarded, not saved as code in your repository.

For that reason, in team scenarios, it works best to merge to shared databases.
