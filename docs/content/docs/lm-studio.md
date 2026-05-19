---
title: Local LLMs
weight: 5
prev: docs/getting-started
next: docs/try-it-out
toc: false
author: Callum
keywords:
  - LM Studio
  - local LLM
  - open-weight
  - self-hosted
---

// TODO : Suggest https://lmstudio.ai/models/qwen3

[LM Studio](https://lmstudio.ai) is a desktop app for running open-weight
models on your own machine. It speaks MCP, so once you point it at the
Rhino server a local model can drive Rhino the same way a hosted one can.

If you're choosing between assistants and aren't sure, start with [Claude
Desktop](../connector); it's the gentler entry point. Pick LM Studio
when you'd rather keep everything on your own hardware.

## Before you start

1. The **Rhino-MCP-Platform** plugin is installed in Rhino. See
   [Getting Started](../getting-started) if you haven't done that yet.
2. **LM Studio** is installed, and you've downloaded a model with strong
   tool-use support. Larger instruction-tuned models (e.g. Qwen, Llama,
   Mistral variants in the 14B+ range) tend to drive MCP tools more
   reliably than small models.

## Wire up the Rhino server

1. In Rhino, run the `RhinoMCPConnect` command. It prints the command
   LM Studio needs to launch the Rhino MCP router.
2. In LM Studio, open the **Program** sidebar and click **Install &gt;
   Edit mcp.json** (or open `~/.lmstudio/mcp.json` directly).
3. Add an entry for the Rhino server, pasting the command and args from
   step 1:

   ```json
   {
     "mcpServers": {
       "rhino": {
         "command": "rhino-mcp-router",
         "args": ["--default-version", "8"]
       }
     }
   }
   ```

4. Save the file. LM Studio picks up the change without a restart; the
   `rhino` server should appear in the MCP tool list for any new chat.

> **Pick the Rhino version** by changing the `--default-version` arg.
> Use `8` for Rhino 8, `9` for Rhino 9 WIP.

## Try it out

Load your model in LM Studio, start a new chat with tool use enabled,
and follow the prompts on the [Try it out](../try-it-out) page.

## Tips

- **Tool use must be on.** In the chat sidebar, make sure the `rhino`
  server is toggled on for the session. LM Studio defaults to
  asking before each tool call.
- **Smaller models struggle.** If the model keeps describing what it
  would do instead of calling tools, try a larger or more recent
  instruction-tuned model.
