FROM python:3.12-slim

WORKDIR /app
COPY app.py .

ENV PORT=8080
EXPOSE 8080

# Simple liveness probe hitting the /health endpoint.
HEALTHCHECK --interval=10s --timeout=3s --retries=3 \
  CMD python -c "import urllib.request,sys; sys.exit(0 if urllib.request.urlopen('http://localhost:8080/health').status==200 else 1)" || exit 1

CMD ["python", "app.py"]
