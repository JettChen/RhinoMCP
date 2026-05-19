---
title: Getting Started
weight: 1
next: docs/connector
toc: false
author: Callum
keywords:
  - getting started
  - installation
  - setup
  - AI assistant
---

> This guide assumes you have [Rhino3d](https://www.rhino3d.com/download/) already installed and licensed.

In about ten minutes you'll have your AI assistant making geometry in your
Rhino window. We'll do it in three steps:

## 1. Pick an AI assistant

Any AI assistant that speaks the [Model Context
Protocol](https://modelcontextprotocol.io) can drive Rhino. Choose one of the AI assistants below to continue.

{{< cards >}}

  {{< card link="../connector" title="Claude Desktop" subtitle="The friendly chat app from Anthropic. Easiest to install and use." >}}

  {{< card link="../cc-plugin" title="Claude Code" subtitle="A terminal-based assistant. Ships with ready-made Rhino and Grasshopper agents." >}}

  {{< card link="../codex" title="Codex" subtitle="OpenAI's terminal-based assistant. Point its MCP config at the Rhino router and go." >}}

  {{< card link="../lm-studio" title="Local LLM" subtitle="Run an open-weight model on your own machine and drive Rhino without sending anything to the cloud." >}}

{{< /cards >}}

**Other MCP clients work too.** [Cursor](https://cursor.com) and
[GitHub Copilot in VS Code](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
all speak MCP and can point at the same Rhino server.
