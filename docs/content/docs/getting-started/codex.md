---
title: OpenAI Codex
icon: chatgpt
weight: 5
prev: docs/getting-started
next: docs/try-it-out
toc: false
author: Callum
editor: SteveF
keywords:
  - Codex
  - OpenAI
  - terminal
  - CLI
---

[Codex](https://github.com/openai/codex) is OpenAI's terminal-based AI assistant. It speaks MCP, so once you point it at the Rhino MCP server, it can drive Rhino & Grasshopper the same way Claude can.

If you're choosing between assistants and aren't sure, start with [Claude Desktop](../connector); it's the gentler entry point.

## 1. Install Codex

[Codex](https://github.com/openai/codex) — install and sign in. See the [Codex install guide](https://github.com/openai/codex#installation) if you need it.

## 2. Install the Rhino plugin

{{< yak package="Rhino-MCP-Platform" version="8" >}}
{{< yak package="Rhino-MCP-Platform" version="9" >}}

If that doesn't work you can try the below:

1. Open Rhino 8 (and/or Rhino 9 WIP)
2. Run the `PackageManager` command
3. Search for, and install Rhino-MCP-Platform

## 3. Connect the two

1. In Rhino, run the `MCPConnect` command.
2. On the **Install** tab, find **Codex** and click **Install**.
3. Restart Codex so it picks up the new server.

![The MCPConnect dialog's Install tab, with an Install button beside each detected agent](/images/install-mcp.png)

<blockquote class="page-note">
Run <code>MCPConnect</code> from the Rhino you want the agent to drive (Rhino 8, or Rhino 9 WIP/BETA); the version is wired in automatically. If Codex isn't listed (for example you haven't started it yet), open the <strong>Prompt</strong> tab and paste it into a Codex session, it'll connect itself.
</blockquote>

## Try it out

<blockquote class="page-note">
Start a Codex session and follow the prompts on the <a href="../../try-it-out">Try It Out</a> page.
</blockquote>
