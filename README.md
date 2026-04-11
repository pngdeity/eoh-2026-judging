# Engineering Open House 2026: Judging System

[![CI/CD Pipeline](https://github.com/pngdeity/eoh-2026-judging/actions/workflows/pipeline.yml/badge.svg)](https://github.com/pngdeity/eoh-2026-judging/actions/workflows/pipeline.yml)

The official judging application for Engineering Open House (EOH) 2026. Forging the Future requires an evaluation system that's just as innovative as the exhibits on display. This tool replaces traditional, arbitrary rubric grading with a fast, fair, and mathematically sound relative judging process.

## The Case for Relative Judging

When you have hundreds of brilliant exhibits across campus and dozens of judges, maintaining a consistent standard is a serious challenge. A "7/10" from a harsh judge could represent the exact same quality as a "10/10" from a lenient one. Score creep and baseline drift ruin the integrity of the results. 

Relative judging cuts through the noise by asking a simpler, more natural question: **Is Project A better than Project B?**

### The Mathematics of the Match-Up

By focusing entirely on head-to-head comparisons, we build a directed graph where each exhibit is a node and each comparison is a directed edge.

1. **Cycle Detection (Topological Sort):** The engine uses Kahn's Algorithm to analyze the graph, identifying any logical loops (e.g., A > B > C > A). It ensures the resulting hierarchy is mathematically sound.
2. **Handling Ties (Union-Find):** If two exhibits are deemed equal, a Union-Find data structure groups them into the same equivalence class, effectively treating them as a single tier.
3. **Score Calculation:** Once a valid partial or total order is established, the system distributes scores based on the finalized tiers. You can choose between:
   * **Linear Spacing:** Evenly distributes scores between the top and bottom tiers.
   * **Percentile Ranking:** Assigns scores based on the percentage of exhibits a specific project outperformed.

## For the Judges: How to Use This Tool

We built this application to be as fast and intuitive as possible so you can spend your time talking to students, not fighting with a spreadsheet.

1. **Setup Your Bracket:** Head to the **Setup** tab to define your categories (e.g., Innovation, Impact) and drop in the IDs of the projects you're evaluating. The bulk import feature lets you paste your entire list at once.
2. **Start Comparing:** Go to the **Judging** tab. Select a category and start putting exhibits head-to-head. Our engine tracks the matrix and suggests the most critical missing comparisons to help you build a complete ranking quickly.
3. **Lock In the Results:** The **Results** tab calculates the live standings. As long as there are no logical cycles in your comparisons, you'll see the final, normalized scores ready for the awards ceremony.

## Development & Deployment

This is a standalone .NET 10 Blazor WebAssembly application. It runs entirely in the browser, ensuring rapid response times even under heavy load during the event.

* **Run Locally:** `dotnet run --project src/ContestJudging.Web`
* **Tests:** Execute the xUnit suite via `dotnet test`
* **Formatting:** Enforced by `.editorconfig`. Run `dotnet format` before committing.

## Licensing

The source code in this repository is licensed under the MIT License. 
Any image, logo, or visual assets derived from the EOH website are **Copyright EOH 2026**.
