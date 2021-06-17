# assuming mode is startup to begin with
$visibleContainers = (docker container ls -a --format "{{ json .}}") | ConvertFrom-Json

# need relative path to repo root. as we need to mount it
$repoRoot = "<TODO>"

# docker container ls -a --format -filter "label=blah" -filter "name=blah"

# check set of visible containers for one. Take the first (there should only be one)

# check status of container. if it's running, we're g2g
# if not running, start it via 




