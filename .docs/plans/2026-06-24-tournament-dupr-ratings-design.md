# tournament-dupr-ratings — Design Document

**Date:** 2026-06-24  
**Project:** `tournament-dupr-ratings`  
**Platform:** .NET 8 Console Application

---

## Overview

A .NET 8 console utility that cross-references a pickleball tournament's registered player list (from PickleballTournaments.com) with DUPR ratings for each participant, then outputs the results grouped by team pair to the console and a CSV file.

---

## Inputs

| Input | Source |
|---|---|
| DUPR Bearer Token | Prompted at runtime (masked) |
| Activity ID | Prompted at runtime |
| Zip code of tournament | Prompted at runtime |
| Google Geocoding API key | `GOOGLE_API_KEY` environment variable |

---

## Architecture

**Option B — Structured service classes** was selected. Services are instantiated directly in `Program.cs` (no DI container). Each service owns one external integration.

### Project Structure

```
tournament-dupr-ratings/
├── tournament-dupr-ratings.csproj
├── Program.cs                          # Orchestration entry point
├── Models/
│   ├── EventPlayer.cs                  # PickleballTournaments response shape
│   ├── DuprSearchRequest.cs
│   ├── DuprSearchResult.cs             # DUPR hit with ratings
│   └── TeamResult.cs                   # Final grouped output per team
├── Services/
│   ├── PickleballTournamentsService.cs # Fetches event players
│   ├── GeocodingService.cs             # Zip → lat/lng via Google API
│   └── DuprService.cs                  # Player search + ratings lookup
└── Output/
    ├── ConsoleOutput.cs                # Formatted table to console
    └── CsvOutput.cs                    # Writes results to CSV file
```

---

## Data Flow

```
1. Collect inputs
   ├── Prompt: DUPR Bearer Token (masked input)
   ├── Prompt: Activity ID
   ├── Prompt: Zip code
   └── Read: GOOGLE_API_KEY from environment variable

2. Geocode zip
   └── GeocodingService → Google Geocoding API → (lat, lng)

3. Fetch event players
   └── PickleballTournamentsService → eventPlayers API → List<EventPlayer>
       └── Build unique player list: each EventPlayer yields up to 2 entries
           (playerFullName + playerSlug, partnerFullName + partnerSlug)
           Skip entries where name is blank or partnerDuprActive is false

4. DUPR lookup (per unique player, sequential)
   └── DuprService.Search(name, lat, lng, bearerToken)
       ├── 1 hit  → use it
       ├── 0 hits → mark as "Not Found"
       └── 2+ hits → pause, display options (name, location, age, ratings),
                     prompt user to pick by number or skip

5. Reassemble teams
   └── Pair each EventPlayer's player result + partner result → List<TeamResult>

6. Output
   ├── ConsoleOutput: formatted table grouped by team
   └── CsvOutput: tournament-{activityId}-ratings.csv saved to working directory
```

DUPR lookups are **sequential** (not parallel) so disambiguation prompts do not interleave.

---

## External APIs

### PickleballTournaments — Event Players
- `GET https://pickleballtournaments.com/tournaments/api/eventPlayers?activityId={activityId}&activitySplitId=null`
- No authentication required

### Google Geocoding
- `GET https://maps.googleapis.com/maps/api/geocode/json?address={zip}&key={GOOGLE_API_KEY}`
- Returns latitude and longitude for the provided zip code

### DUPR — Player Search
- `POST https://api.dupr.gg/player/v1.0/search`
- Authorization: `Bearer {token}`
- Body: `{ "limit": 10, "offset": 0, "query": "{fullName}", "exclude": [], "includeUnclaimedPlayers": true, "filter": { "lat": {lat}, "lng": {lng}, "rating": { "maxRating": null, "minRating": null }, "locationText": "" } }`

---

## Models

### `EventPlayer`
Relevant deserialized fields:
- `playerFullName`, `partnerFullName`
- `playerSlug`, `partnerSlug`
- `playerSkill`, `partnerSkill`
- `playerDuprActive`, `partnerDuprActive`
- `needAPartner`

### `DuprSearchResult`
Relevant deserialized fields per hit:
- `id`, `fullName`, `shortAddress`, `age`, `duprId`
- `ratings.doubles`, `ratings.doublesVerified`
- `ratings.singles`, `ratings.singlesVerified`

### `TeamResult`
Final assembled output per team:
- `Player1Name`, `Player1DuprId`, `Player1Doubles`, `Player1Singles`
- `Player2Name`, `Player2DuprId`, `Player2Doubles`, `Player2Singles`
- Status flags: `NotFound`, `Skipped`, `NA` per player slot

---

## Output

### Console Table
Formatted table printed after all lookups complete, columns mirroring CSV.

### CSV File
Filename: `tournament-{activityId}-ratings.csv`  
Saved to: current working directory

**Columns:**
```
Team, Player1, Player1 DUPR ID, Player1 Doubles, Player1 Singles,
      Player2, Player2 DUPR ID, Player2 Doubles, Player2 Singles
```

**Display conventions:**
- Unrated: `"NR"`
- Not found in DUPR: `"Not Found"`
- Skipped during disambiguation: `"Skipped"`
- No partner (singles or inactive): `"N/A"`

---

## Disambiguation Flow

When DUPR returns 2+ hits for a player name:
1. Print all hits with index number, showing: full name, location, age, doubles rating, singles rating
2. Prompt: `"Enter number to select, or 0 to skip:"`
3. Selected hit is used; skip records `"Skipped"` for that slot

---

## Error Handling

| Scenario | Behavior |
|---|---|
| `GOOGLE_API_KEY` not set | Exit immediately before prompting user |
| Any HTTP failure | Display status code + error body, exit |
| Geocoding returns no results | Prompt user to re-enter zip code |
| Empty player list from tournament | Exit with descriptive message |
| DUPR returns 401 | Exit with "Invalid or expired Bearer Token" |
| Player skipped during disambiguation | Recorded as `"Skipped"`, processing continues |
| Blank partner name or `partnerDuprActive: false` | Recorded as `"N/A"`, no DUPR request made |
