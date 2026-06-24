# Tournament DUPR Ratings

A .NET 8 console app that fetches player rosters from a Pickleball Tournaments event and looks up each player's DUPR singles and doubles ratings, outputting the results to the console and a CSV file.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A **DUPR Bearer Token** (obtain from your DUPR account)
- A **Google Geocoding API key** with the Geocoding API enabled

## Setup

Set the Google API key as an environment variable:

```bash
# Windows (Command Prompt)
set GOOGLE_API_KEY=your_key_here

# Windows (PowerShell)
$env:GOOGLE_API_KEY = "your_key_here"
```

## Usage

### Interactive

Run without arguments and the app will prompt for each value:

```bash
dotnet run
```

```
DUPR Bearer Token: ********
Activity ID: 12345
Tournament zip code: 98101
```

### With launch arguments

Supply any combination of arguments to skip the corresponding prompts:

```bash
dotnet run -- --bearer-token <token> --activity-id <id> --zip <zip>
```

| Argument | Description |
|---|---|
| `--bearer-token` | DUPR API bearer token |
| `--activity-id` | Pickleball Tournaments event/activity ID |
| `--zip` | Tournament zip code (used to bias DUPR search results by location) |

### VS Code

Fill in the values in `.vscode/launch.json` under the `args` and `env` fields, then press **F5**.

## Output

- **Console** — a formatted table of teams and ratings
- **CSV** — `tournament-{activityId}-ratings.csv` written to the working directory

### CSV columns

`Team`, `Player1`, `Player1 DUPR ID`, `Player1 Doubles`, `Player1 Singles`, `Player2`, `Player2 DUPR ID`, `Player2 Doubles`, `Player2 Singles`

Rating values show `NR` (not rated) when a player has no rating on record, or `Not Found` / `Skipped` when the player could not be matched in DUPR.

## Disambiguation

When a player name returns multiple DUPR matches you will be prompted to pick one:

```
3 matches — please select:
  1. Jane Doe | Seattle, WA | Age: 34 | Dbl: 4.12 | Sgl: NR
  2. Jane Doe | Portland, OR | Age: 28 | Dbl: 3.87 | Sgl: 3.91
  3. Jane M. Doe | Seattle, WA | Age: 34 | Dbl: NR | Sgl: NR
Enter number to select, or 0 to skip:
```
