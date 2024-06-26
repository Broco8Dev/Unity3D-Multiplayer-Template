import asyncio
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import uvicorn
from typing import Dict
import socket
from contextlib import asynccontextmanager

positions: Dict[str, str] = {}

class KeyValue(BaseModel):
    key: str
    value: str

@asynccontextmanager
async def lifespan(app: FastAPI):
    udp_server = UDPServer()
    udp_task = asyncio.create_task(udp_server.run())
    yield
    udp_task.cancel()
    try:
        await udp_task
    except asyncio.CancelledError:
        pass
    finally:
        udp_server.close()

app = FastAPI(lifespan=lifespan)

class UDPServer:
    def __init__(self):
        self.UDP_IP = "0.0.0.0"
        self.UDP_PORT = 5000
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.setblocking(False)

    async def run(self):
        self.sock.bind((self.UDP_IP, self.UDP_PORT))
        print(f"UDP server listening on {self.UDP_IP}:{self.UDP_PORT}")
        while True:
            try:
                data, addr = await asyncio.get_event_loop().sock_recvfrom(self.sock, 1024)
                message = data.decode('utf-8')
                key, value = message.split(':', 1)
                positions[key] = value
            except Exception as e:
                print(f"Error in UDP server: {e}")
                await asyncio.sleep(0.1)

    def close(self):
        self.sock.close()

@app.post("/upload")
async def upload_key_value(data: KeyValue):
    positions[data.key] = data.value
    return {"message": f"Key '{data.key}' updated with value '{data.value}'."}

@app.get("/get")
async def get_dict():
    return positions

@app.post("/delete")
async def delete_key_value(data: KeyValue):
    if data.key in positions:
        del positions[data.key]
        return {"message": f"Key '{data.key}' deleted."}
    raise HTTPException(status_code=404, detail=f"Key '{data.key}' not found in the dictionary.")

if __name__ == "__main__":
    uvicorn.run("server:app", host="0.0.0.0", port=5000)