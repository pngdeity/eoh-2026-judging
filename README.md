# EOH 2026 Relative Judging

[![CI/CD](https://github.com/pngdeity/eoh-2026-judging/actions/workflows/ci.yml/badge.svg)](https://github.com/pngdeity/eoh-2026-judging/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512bd4?logo=dotnet)

Judging forty exhibits by hand is an exercise in slow drift. By the end, the
numbers blur together.

This tool replaces the rubric with one question, asked relentlessly: **Is
Project A better than Project B?**

---

## How It Works

You split the field into groups. A few exhibits cross every boundary — our
**Bridge Nodes** — holding the groups together like thread through cloth.

Each comparison feeds a **Bradley-Terry model**, a machine that watches the
shape of your choices and learns what you meant. It adjusts for the fact that
your eye wanders.

At the end, it returns a ranking. No inflation, no drift — just the order the
data demands.

---

## The Flow

**Setup** — Name your categories. Paste your roster.

**Judging** — Compare head-to-head. The system suggests the next pair.

**Results** — One click. The standings resolve.

---

## Running It

A Blazor WebAssembly app. Nothing to install.

```
dotnet run --project src/ContestJudging.Web
dotnet test
dotnet format
```

MIT License.
