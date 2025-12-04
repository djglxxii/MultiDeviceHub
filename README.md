# Multi-Device-Hub (MDH) TCP Service - Proof of Concept

A .NET 9 proof-of-concept solution for the Multi-Device-Hub (MDH) service that implements a TCP listener for POCT1A devices with session management, handler pipelines, and automatic ACK generation.

## Overview

The MDH service provides:

- **TCP Listener**: Accepts connections from POCT1A devices (no TLS, no authentication)
- **Session Engine**: Manages device sessions with:
  - Automatic device detection from first XML message
  - Vendor pack binding
  - Handler pipeline execution per inbound message
  - Automatic ACK generation based on vendor-defined rules
  - Stack-of-queues outbound model for message ordering
- **Demo Vendor Pack**: Example implementation showing:
  - Device matching logic
  - Handler pipeline
  - Message ACK rules registry
  - POCT1A flows for HEL, DST, REQ, OBS, EOT, and list updates (OPL/PTL)

## Solution Structure

```
/src
  /Mdh.Core                    # Core abstractions and engine
  /Mdh.Protocol.Poct1A         # POCT1A protocol utilities
  /Mdh.Vendor.Demo             # Demo vendor pack implementation
  /Mdh.Host.Tcp                # TCP host executable
```

### Mdh.Core

Core abstractions and session engine:

- **Sessions**: `SessionContext`, `AckErrorState`, `TerminationState`
- **Outbound**: `OutboundMessage`, `OutboundFrame`, status types
- **Engine**: `SessionEngine`, handler interfaces (`IHandler`, `IOutboundHandler`)
- **Vendors**: Vendor pack abstractions (`IVendorDevicePack`, `IPoct1AVendorPack`, `IAckRuleRegistry`)
- **Inbound**: Message history tracking

### Mdh.Protocol.Poct1A

POCT1A-specific utilities:

- **Poct1AParser**: Extracts message type and control ID from XML
- **AckBuilder**: Builds ACK.R01 messages
- **MessageBuilders**: Builds REQ, OPL, PTL, EOT, END messages

### Mdh.Vendor.Demo

Demo vendor pack implementation:

- **DemoVendorPack**: Matches POCT1A devices and builds handler pipeline
- **DemoAckRuleRegistry**: Defines which messages require ACKs
- **Handlers**:
  - `HelloHandler`: Processes HEL.R01 messages
  - `DeviceStatusHandler`: Processes DST.R01 and extracts new observations count
  - `ObservationRequestHandler`: Sends REQ.R01 when new observations are available
  - `ObservationHandler`: Processes OBS.R01 and EOT.R01 messages
  - `ListUploadHandler`: Sends OPL.R01 and PTL.R01 list updates

### Mdh.Host.Tcp

TCP host application:

- **TcpListenerHostedService**: Listens for TCP connections and manages sessions
- **Program.cs**: Sets up dependency injection and starts the host
- **appsettings.json**: Configuration (default port: 5000)

## Building and Running

### Prerequisites

- .NET 9 SDK

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run --project src/Mdh.Host.Tcp
```

The service will start listening on port 5000 (configurable via `appsettings.json`).

## Configuration

Edit `src/Mdh.Host.Tcp/appsettings.json` to change the listen port:

```json
{
  "Mdh": {
    "ListenPort": 5000
  }
}
```

## Testing

### Using a TCP Client

You can test the service using any TCP client. Here are some examples:

#### Using `nc` (netcat)

```bash
nc localhost 5000
```

Then paste POCT1A XML messages, for example:

```xml
<HEL.R01>
  <HDR>
    <HDR.message_type V="HEL.R01" />
    <HDR.control_id V="1" />
    <HDR.version_id V="POCT1" />
    <HDR.creation_dttm V="2025-01-01T00:00:00Z" />
  </HDR>
  <HEL>
    <HEL.device_id V="DEV001" />
    <HEL.version V="1.0" />
  </HEL>
</HEL.R01>
```

#### Using `telnet`

```bash
telnet localhost 5000
```

#### Using a Custom TCP Tool

Any TCP client that can send UTF-8 encoded XML messages will work.

### Example POCT1A Message Flow

1. **HEL.R01** (Hello):
   ```xml
   <HEL.R01>
     <HDR>
       <HDR.message_type V="HEL.R01" />
       <HDR.control_id V="1" />
       <HDR.version_id V="POCT1" />
       <HDR.creation_dttm V="2025-01-01T00:00:00Z" />
     </HDR>
     <HEL>
       <HEL.device_id V="DEV001" />
     </HEL>
   </HEL.R01>
   ```

2. **DST.R01** (Device Status):
   ```xml
   <DST.R01>
     <HDR>
       <HDR.message_type V="DST.R01" />
       <HDR.control_id V="2" />
       <HDR.version_id V="POCT1" />
       <HDR.creation_dttm V="2025-01-01T00:00:01Z" />
     </HDR>
     <DST>
       <DST.new_observations_qty V="5" />
     </DST>
   </DST.R01>
   ```

   This will trigger:
   - REQ.R01 (request observations)
   - OPL.R01 messages (operator list)
   - PTL.R01 messages (patient list)

3. **OBS.R01** (Observation):
   ```xml
   <OBS.R01>
     <HDR>
       <HDR.message_type V="OBS.R01" />
       <HDR.control_id V="3" />
       <HDR.version_id V="POCT1" />
       <HDR.creation_dttm V="2025-01-01T00:00:02Z" />
     </HDR>
     <OBS>
       <OBS.observation_id V="OBS001" />
       <OBS.test_id V="GLU" />
       <OBS.result V="100" />
     </OBS>
   </OBS.R01>
   ```

4. **EOT.R01** (End of Topic):
   ```xml
   <EOT.R01>
     <HDR>
       <HDR.message_type V="EOT.R01" />
       <HDR.control_id V="4" />
       <HDR.version_id V="POCT1" />
       <HDR.creation_dttm V="2025-01-01T00:00:03Z" />
     </HDR>
     <EOT>
       <EOT.topic V="OBS" />
     </EOT>
   </EOT.R01>
   ```

### Expected Console Output

When running the service, you should see logs like:

```
[Information] MDH TCP listener started on port 5000
[Information] Session {guid} connected from {endpoint}
[Information] Session {guid} matched vendor pack: Demo POCT1A Device
[Information] Session {guid} received inbound HEL.R01 (ControlId: 1)
[Information] Session {guid} queued ACK AA for HEL.R01 (ControlId: 1)
[Information] Session {guid} sent outbound ACK.R01 (ControlId: 1, AckRequired: False)
[Information] Session {guid} received inbound DST.R01 (ControlId: 2)
[Information] Session {guid} device reports 5 new observations
[Information] Session {guid} queued REQ.R01 (ControlId: 2000)
[Information] Session {guid} sent outbound REQ.R01 (ControlId: 2000, AckRequired: True)
...
```

## Architecture Notes

### Session Engine

The `SessionEngine` runs two concurrent loops:

1. **Inbound Loop**: Reads XML messages, parses metadata, runs handler pipeline, and generates ACKs
2. **Outbound Loop**: Processes the outbound stack, sends messages, and correlates ACKs

### Outbound Stack Model

Messages are organized in frames (queues) on a stack:

- Frames are processed LIFO (last-in-first-out)
- Messages within a frame are processed FIFO (first-in-first-out)
- This ensures proper ordering for multi-message operations (e.g., list uploads)

### ACK Handling

- ACKs are automatically generated based on vendor-defined rules
- The engine waits for ACKs when `AckRequired = true`
- ACK correlation matches `ack_control_id` to the original message's `control_id`
- Timeout handling (30 seconds) for missing ACKs

### Handler Pipeline

Handlers are executed in order defined by the vendor pack:

1. `HelloHandler`
2. `DeviceStatusHandler`
3. `ObservationRequestHandler`
4. `ObservationHandler`
5. `ListUploadHandler`

Each handler can:
- Inspect/modify `SessionContext`
- Push outbound frames to `OutboundStack`
- Report errors via `AckErrorState`
- Implement `IOutboundHandler` to receive completion callbacks

## Limitations (Proof of Concept)

This is a proof-of-concept implementation with the following limitations:

- **No TLS/SSL**: Plain TCP connections only
- **No Authentication**: No device authentication or authorization
- **No Certificates**: No certificate validation
- **Simplified ACK Matching**: Assumes one outstanding ACK-required message at a time
- **Basic XML Parsing**: Simple XML framing (read until root tag closes)
- **UTF-8 Only**: Assumes UTF-8 encoding
- **Single Protocol**: Only POCT1A is supported (though architecture supports multiple)

## Extending the Solution

To add a new vendor pack:

1. Create a new project (e.g., `Mdh.Vendor.YourVendor`)
2. Implement `IPoct1AVendorPack` or `IVendorDevicePack`
3. Implement `IAckRuleRegistry` for ACK rules
4. Create handlers implementing `IHandler` (and optionally `IOutboundHandler`)
5. Register the vendor pack in `Program.cs`

## License

This is a proof-of-concept implementation for demonstration purposes.
