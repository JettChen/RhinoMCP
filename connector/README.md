# Claude Desktop Connector

## Pre-requisites

`npm install -g @anthropic-ai/mcpb`

## Build

Run `mcpb pack` in the connector directory.

## Install

- Double click the `.mcpb` file.
- If you have already installed it, you will need to run uninstall and then double click again.

## Reading

- https://claude.com/docs/connectors/building/mcpb

## Privacy Policy

The connector runs entirely on the user's machine and does not collect, log, or transmit data on its own. Tool results are returned to Claude through the MCP channel and handled under Anthropic's privacy policy. Some tools (`run_command`, `run_python`, `run_csharp`, `open_doc`, `save_doc`) can execute code or touch files the user's account has access to.

Full policy: https://github.com/mcneel/RhinoMCP/blob/main/PRIVACY.md
