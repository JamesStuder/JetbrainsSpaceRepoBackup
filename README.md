# JetbrainsSpaceRepoBackup
Console App To back up ALL Project Repo's you have access too.  This will loop over all projects that you have access to and clone / pull the repos.  There will be a subdirectory created for each project and each repo will be placed in the project's subdirectory.

# Steps:
1. Get your Jetbrains Space Url
2. Generate a permanent user token in Space
3. Clone project
4. Build project

** You will have the option to either store the needed information as User Environment Variables or you can pass them as arguments.  I would recommend using the environment variables since the token is long. **

5. Run Program or Schedule it.
