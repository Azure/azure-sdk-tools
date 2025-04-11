nohup go run . > service.log 2>&1 &
ngrok http 8088 --url https://neutral-pleasant-gecko.ngrok-free.app > ngrok.log 2>&1
