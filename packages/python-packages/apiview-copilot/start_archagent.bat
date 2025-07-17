@echo off
REM Activate your virtual environment here if needed
REM call .venv\Scripts\activate
uvicorn app:app --reload --host 127.0.0.1 --port 8080 --log-level debug
