# Standing Rules

- For anything Revit-related, use the `765t-flow` server.
- The server's operating rules arrive with the connection, in the MCP `instructions` field at handshake — read them once and keep them. And for any Revit task you are not already sure how to carry out — which tools exist, in which order, under which gates — call `tool_get_guidance` first, with the task as `Query` and the `instanceId` from `flow_list_instances`; it answers the curated plan and the Revit operations rules for that task. Do not draw, model or mutate through the server before that grounding is in hand.
