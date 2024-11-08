using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace Space.ProjectRepoBackup
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Retrieve arguments or environment variables if not provided as arguments
            string? spaceUrl = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("SPACE_URL", EnvironmentVariableTarget.User);
            string? bearerToken = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("SPACE_BEARER_TOKEN", EnvironmentVariableTarget.User);
            string? cloneDirectory = args.Length > 2 ? args[2] : Environment.GetEnvironmentVariable("SPACE_CLONE_DIRECTORY", EnvironmentVariableTarget.User);
            string? emailForPull = args.Length > 3 ? args[3] : Environment.GetEnvironmentVariable("SPACE_EMAIL_FOR_PULL", EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(spaceUrl) || string.IsNullOrEmpty(bearerToken) || string.IsNullOrEmpty(cloneDirectory) || string.IsNullOrEmpty(emailForPull))
            {
                Console.WriteLine("Missing required information. Please provide all necessary arguments or set the appropriate environment variables.");
                return;
            }

            // Retrieve project names
            List<(string Id, string Name)> projects = await GetProjectsAsync(spaceUrl, bearerToken);

            // Loop through each project and clone or pull repositories
            foreach ((string Id, string Name) project in projects)
            {
                string projectDirectory = Path.Combine(cloneDirectory, project.Name);
                Directory.CreateDirectory(projectDirectory);

                List<string> repoNames = await GetRepositoryNamesAsync(spaceUrl, project.Id, bearerToken);

                foreach (string repoName in repoNames)
                {
                    string? cloneUrl = await GetCloneUrlAsync(spaceUrl, project.Id, repoName, bearerToken);
                    if (string.IsNullOrEmpty(cloneUrl))
                    {
                        Console.WriteLine($"Failed to get clone URL for repository: {repoName}");
                        continue;
                    }

                    string repoPath = Path.Combine(projectDirectory, repoName);

                    Console.WriteLine($"Processing repository: {repoName}");

                    if (Directory.Exists(repoPath))
                    {
                        Console.WriteLine($"Repository {repoName} already exists. Pulling latest changes...");
                        PullRepository(repoPath, bearerToken, emailForPull);
                    }
                    else
                    {
                        Console.WriteLine($"Cloning repository {repoName} from {cloneUrl}...");
                        CloneRepository(cloneUrl, repoPath, bearerToken);
                    }
                }
            }
        }

        private static async Task<List<(string Id, string Name)>> GetProjectsAsync(string? spaceUrl, string? bearerToken)
        {
            List<(string Id, string Name)> projects = new List<(string Id, string Name)>();
            using HttpClient client = new ();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            string url = $"{spaceUrl}/api/http/projects?$fields=data(id,name)";
            HttpResponseMessage response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                JsonDocument jsonDoc = JsonDocument.Parse(jsonResponse);
                if (jsonDoc.RootElement.TryGetProperty("data", out JsonElement projectsElement))
                {
                    foreach (JsonElement project in projectsElement.EnumerateArray())
                    {
                        string? id = project.GetProperty("id").GetString();
                        string? name = project.GetProperty("name").GetString();
                        if (id != null && name != null) projects.Add((id, name));
                    }
                }
            }
            else
            {
                Console.WriteLine($"Failed to retrieve project IDs: {response.ReasonPhrase}");
            }

            return projects;
        }

        private static async Task<List<string>> GetRepositoryNamesAsync(string? spaceUrl, string? projectId, string? bearerToken)
        {
            List<string> repoNames = new List<string>();
            using HttpClient client = new ();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            string url = $"{spaceUrl}/api/http/projects/id:{projectId}?$fields=repos(name)";
            HttpResponseMessage response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                JsonDocument jsonDoc = JsonDocument.Parse(jsonResponse);
                if (jsonDoc.RootElement.TryGetProperty("repos", out JsonElement reposElement))
                {
                    foreach (JsonElement repo in reposElement.EnumerateArray())
                    {
                        string? name = repo.GetProperty("name").GetString();
                        if (name != null) repoNames.Add(name);
                    }
                }
            }
            else
            {
                Console.WriteLine($"Failed to retrieve repositories: {response.ReasonPhrase}");
            }

            return repoNames;
        }

        private static async Task<string?> GetCloneUrlAsync(string? spaceUrl, string? projectId, string repoName, string? bearerToken)
        {
            using HttpClient client = new ();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            string url = $"{spaceUrl}/api/http/projects/id:{projectId}/repositories/{repoName}/url";
            HttpResponseMessage response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                JsonDocument jsonDoc = JsonDocument.Parse(jsonResponse);
                if (jsonDoc.RootElement.TryGetProperty("httpUrl", out JsonElement cloneUrlElement))
                {
                    return cloneUrlElement.GetString();
                }
            }
            else
            {
                Console.WriteLine($"Failed to get clone URL for repository {repoName}: {response.ReasonPhrase}");
            }

            return null;
        }

        private static void CloneRepository(string? cloneUrl, string repoPath, string? bearerToken)
        {
            try
            {
                CloneOptions options = new ()
                {
                    FetchOptions =
                    {
                        CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "bearer", Password = bearerToken }
                    }
                };
                Repository.Clone(cloneUrl, repoPath, options);
                Console.WriteLine($"Cloned {cloneUrl} to {repoPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clone repository: {ex.Message}");
            }
        }

        private static void PullRepository(string repoPath, string? bearerToken, string? emailForPull)
        {
            try
            {
                using Repository repo = new (repoPath);
                PullOptions options = new()
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "bearer", Password = bearerToken }
                    }
                };
                Commands.Pull(repo, new Signature("Automated Pull", emailForPull, DateTimeOffset.Now), options);
                Console.WriteLine($"Pulled latest changes for {repoPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to pull repository: {ex.Message}");
            }
        }
    }
}