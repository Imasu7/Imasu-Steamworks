# Imasu Steamworks Dedicated Server — headless Linux container.
#
# 1. Build the server: Imasu → Steamworks → Dedicated → Build Linux Headless Server…
#    Point the output folder at deploy/ in this package.
# 2. Then:
#      docker build -t imasu-dedicated .
#      docker run --rm -p 7777:7777/udp imasu-dedicated --port 7777
#
# The Linux headless build is graphics-free (StandaloneBuildSubtarget.Server), so a
# dotnet runtime-deps base image is enough.
#
# NOTE: this is a working stub, not a hardened production image (see docs/deploy.md).

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-jammy

WORKDIR /server
COPY deploy/ ./
RUN chmod +x DedicatedServer.x86_64

EXPOSE 7777/udp

ENTRYPOINT ["./DedicatedServer.x86_64", "-batchmode", "-nographics", "-imasuDedicated"]