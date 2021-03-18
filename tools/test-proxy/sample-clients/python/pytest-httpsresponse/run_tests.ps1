$Env:AZURE_RECORD_MODE = "record"
pytest test.py
$Env:AZURE_RECORD_MODE = "playback"
pytest test.py