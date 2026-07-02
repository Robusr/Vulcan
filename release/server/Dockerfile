# Vulcan Server — Docker deployment
# Build:  docker build -t vulcan-server .
# Run:    docker run -p 5000:5000 --env-file .env vulcan-server

FROM python:3.9-slim

WORKDIR /app

COPY requirements.txt .

RUN pip install --no-cache-dir -r requirements.txt -i https://pypi.tuna.tsinghua.edu.cn/simple

COPY . .

EXPOSE 5000

CMD ["python3", "app.py"]
