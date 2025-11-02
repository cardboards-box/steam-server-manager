@echo off

docker build . --tag tss -f ./test.DockerFile
docker run -d tss