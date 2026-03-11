# install tools
sudo apt update
sudo apt install make g++ python3 golang-go postgresql-client libcap-dev -y

# setup isolate
cd isolate
make isolate
mv isolate ../Exesh/isolate
cd ../
#sudo mkdir -p /sys/fs/cgroup/isolate

# install docker
sudo apt install ca-certificates curl gnupg software-properties-common -y
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt update
sudo apt install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin -y

# password required
docker login -u divancode74
