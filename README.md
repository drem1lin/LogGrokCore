# LogGrokCore

[![Unit tests](https://github.com/drem1lin/LogGrokCore/actions/workflows/run-tests.yml/badge.svg)](https://github.com/drem1lin/LogGrokCore/actions/workflows/run-tests.yml)
[![Upload Binaries](https://github.com/drem1lin/LogGrokCore/actions/workflows/build_upload.yml/badge.svg)](https://github.com/drem1lin/LogGrokCore/actions/workflows/build_upload.yml)

A fast, lightweight WPF log viewer with virtualized rendering, regex search, and customizable filters.

## Features

- **High-performance log parsing** — handles large files with streaming and indexing
- **Virtualized UI** — smoothly scrolls through millions of lines
- **Regex search** — find patterns across the entire log
- **Filterable columns** — include/exclude log entries by component values
- **Multiple encodings** — auto-detects UTF-8, UTF-16 LE/BE, and more
- **Multi-instance support** — opens additional files in the running instance

## Requirements

- Windows 10+ 
- .NET 7.0 Runtime

## Building

```bash
dotnet restore
dotnet build --configuration Release
```

## Running Tests

```bash
dotnet test --verbosity normal
```
