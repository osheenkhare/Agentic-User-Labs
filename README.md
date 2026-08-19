# Agent-Identity-Labs
Hands-on labs and sample implementations for building, configuring, and extending agentic users with Microsoft 365 identity and capabilities.

# Lab modules

| Module | Outcome | What gets built / learned | Dev Efforts (Human led) | Dev Efforts (Copilot led) |
|---|---|---|---|---|
| 0. Get Ready to Build | A working local development environment where the agent can be built and tested. | Install dependencies; configure the environment; use Agent Playground for local testing | 40 min | 15 min |
| 1. Create a Discoverable Digital Worker | A provisioned agentic user / Digital Worker that users can discover across Microsoft 365. | Set up AB, AI, AU, and required licenses; verify discovery in Teams and WXP | 45 min | 20 min |
| 2. Make the Agent Respond | A user can message the agent in WXPTO and receive a response. | Scaffold basic agent code; create a dev tunnel and callback URL; configure callback URL/TDP; connect through ACF and Graph | 55 min | 25 min |
| 3. Stream Responses | The agent streams its response to the user instead of waiting for the complete response. | Implement streaming responses and validate the end-to-end experience | 40 min | 15 min |
| 4. Create Rich Responses | The agent can return rich, structured responses appropriate to the interaction. | Use COT, Adaptive Cards, and Markdown in responses | 45 min | 20 min |
| 5. React to Microsoft Graph Events | The agent can receive a Graph change notification and take action in response. | Acquire a Graph token; receive and process Graph API change notifications; invoke Graph APIs based on the event | 50 min | 25 min |
| 6. Act on Behalf of the User | The agent can securely call downstream services using the signed-in user's identity. | Implement and validate the OBO flow | 45 min | 20 min |
| 7. Handle Lifecycle Events | The agent correctly responds to important lifecycle changes. | Receive and process lifecycle events; implement appropriate lifecycle handling | 40 min | 20 min |
| 8. Participate Naturally in Conversations | The agent can react to messages and reply when relevant. | React to text/messages; generate contextual replies | 40 min | 15 min |
| 9. Participate in Channels and Threads | The agent can participate correctly in channel conversations and threaded discussions. | Handle channel messages; create threaded replies; preserve conversation context | 45 min | 20 min |
| 10. Migrate an Existing Digital Worker | An existing Digital Worker is migrated to the Agentic User model without disrupting its existing experience. | Create the new DW/AU; release the previous email identity; update existing logic to point to the new identity/person via deep link or GC | Implementation-dependent | Implementation-dependent |

# Pre requsites
- [Microsoft dev-tunnel](./dependencies/dev-tunnel/README.md)
- [Microsoft Graph Explorer](./dependencies/graph-explorer/README.md)
