using System.Text.RegularExpressions;
using Octokit;
enum Mode { Open, Merge, NoMerge }

class Program
{
    static readonly string[] ValidArgs = { "open", "merge", "nomerge" };
    const int IssueNumber = 5;

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || !ValidArgs.Contains(args[0].ToLower()))
        {
            await Console.Error.WriteLineAsync("Oczekiwano jeden z argumentow open|merge|nomerge");
            return 1;
        }

        var mode = args[0].ToLower()
            switch
        {
            "open" => Mode.Open,
            "merge" => Mode.Merge,
            "nomerge" => Mode.NoMerge,
            _ => throw new ArgumentOutOfRangeException()
        };

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var repoId = long.Parse(Environment.GetEnvironmentVariable("REPO_ID"));
        var prNumber = int.Parse(Environment.GetEnvironmentVariable("PR_NUMBER"));

        if (string.IsNullOrEmpty(token))
        {
            await Console.Error.WriteLineAsync("Brak tokena GITHUB_TOKEN");
            return 1;
        }

        if (repoId == null)
        {
            await Console.Error.WriteLineAsync("Brak repozytorium REPO_ID");
            return 1;
        }

        if (prNumber == null)
        {
            await Console.Error.WriteLineAsync("Brak numeru PR PR_NUMBER");
            return 1;
        }

        Console.WriteLine($"Sprawdzam PR #{prNumber} w trybie {mode}");

        var client = new GitHubClient(new ProductHeaderValue("update-checklist-action"))
        {
            Credentials = new Credentials(token)
        };

        var issue = await client.Issue.Get(repoId, IssueNumber);
        var pr = await client.PullRequest.Get(repoId, prNumber);
        var prAuthor = pr.User.Login;
        var files = await client.PullRequest.Files(repoId, prNumber);
        var paths = files.Select(f => f.FileName).ToList(); // nazwy plików

        var updatedBody = UpdateChecklist(
            issue.Body ?? string.Empty,
            paths,
            prAuthor,
            prNumber,
            mode);

        if (updatedBody != issue.Body)
        {
            var update = issue.ToUpdate();
            update.Body = updatedBody;
            await client.Issue.Update(repoId, IssueNumber, update);
            Console.WriteLine("Zaktualizowano zawartość issue");
        }
        else
        {
            Console.WriteLine("Nie wprowadzono zmian");
        }

        return 0;
    }


    // Modlę się do Ducha Swiętego aby poniższa funkcja nie popsuła treści issue
    private static string UpdateChecklist(string body, List<string> paths, string author, int prNumber, Mode mode)
    {
        var element = new Regex(@"(?m)^(?<front>\s*)- \[(?<box>[ xX])\] (?<name>[^\s]+)(?<rest>.*)$");

        /*
         * (?m) - tryb wielowierszowywy
         * ^ - początek regexu
         * (?<front>\s*) - grupa o nazwie ?<front> (0 lub więcej spacji \s na początku linii)
         * (?<box>[ xX]) - grupa o nazwie ?<box> zawierająca "x", "X" lub nic.
         * -, \[, \] - po prostu symbole
         * (?<rest>.*) - grupa ?<rest> zawierająca dowolną ilość pozostałych symboli
         */

        var componentNames = body.Split('\n')
            .Select(l => element.Match(l))
            .Where(m => m.Success)
            .Select(m => m.Groups["name"].Value)
            .ToList();

        var result = body;
        foreach (var fullPath in paths)
        {
            var parts = fullPath.Split('/');

            // szukamy "część wspólną" między elementami ścieżki a elementami checklistu
            var comp = componentNames.Intersect(parts).FirstOrDefault();

            if (comp == null)
                continue;

            result = element.Replace(result,
                match =>
                {
                    if (!string.Equals(match.Groups["name"].Value, comp, StringComparison.Ordinal))
                        return match.Value;

                    var front = match.Groups["front"].Value;
                    var rest = match.Groups["rest"].Value;

                    var line = $"{front}- [ ] {comp}";

                    rest = Regex.Replace(rest, @"\(w trakcie:[^)]*\)", "");
                    rest = Regex.Replace(rest, @"\(#\d+(,\s*#\d+)*\)", "");

                    if (mode == Mode.Open)
                    {
                        line += $" (w trakcie: {author})";
                    }
                    else if (mode == Mode.Merge)
                    {
                        line += $" (#{prNumber})";
                    }
                    // nomerge - nie ma co aktualizować

                    return line + rest;
                });
        }

        return result;
    }
}
