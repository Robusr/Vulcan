from fastapi import FastAPI
from dotenv import load_dotenv
import os
from api.v1 import agent

load_dotenv()

app = FastAPI(title="SolidWorks AI Agent Server")
app.include_router(agent.router, prefix="/api/v1/agent")

@app.get("/health")
async def health_check():
    return {"status": "ok", "model": os.getenv("MODEL_NAME")}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "main:app",
        host=os.getenv("HOST", "0.0.0.0"),
        port=int(os.getenv("PORT", 8000)),
        reload=True
    )