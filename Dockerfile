FROM python:3.14.3-slim-bookworm

WORKDIR /app
COPY . .
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

CMD ["python", "src/Main.py"]