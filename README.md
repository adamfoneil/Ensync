This is a reboot of my [ModelSync](https://github.com/adamfoneil/ModelSync) project, to break from the NETStandard2 dependency, refactor the diff engine with fresh eyes, and to rebuild all the tooling. There will be a console app for scripting and merging code-first entities. The long term vision is to have a WPF app remake of [ModelSync UI](https://aosoftware.net/modelsync/).

# Why?
I like code-first database development, but I never made peace with EF migrations. I find them way too fussy and complicated. I'm looking for a more fluid and effortless experience around entity development. That's what this project is about.

A "fluid" experience does come with a trade-off, however. In a team environment, this library is not intended for merging to a local database. Although you can share the generated diff scripts with collaborators, people must run the scripts themselves as opposed to having your application run them the way Entity Framework does.

So, the best use case for this is to merge to shared databases or local databases in a solo project.
