package docker

import (
	"slices"

	"github.com/docker/docker/api/types/container"
	"github.com/docker/docker/api/types/network"
)

type policy func(hc *container.HostConfig)

func ulimitPolicy(ul *container.Ulimit) policy {
	return func(hc *container.HostConfig) {
		idx := slices.IndexFunc(hc.Ulimits, func(ul *container.Ulimit) bool { return ul.Name == "nproc" })
		if idx < 0 {
			idx = len(hc.Ulimits)
			hc.Ulimits = append(hc.Ulimits, nil)
		}
		hc.Ulimits[idx] = ul
	}
}

func noopPolicy(hc *container.HostConfig) {}

func cpuPolicy(timeS int64) policy {
	if timeS == 0 {
		return noopPolicy
	}
	return ulimitPolicy(&container.Ulimit{Name: "cpu", Soft: timeS, Hard: timeS})
}

func memoryPolicy(memoryB int64) policy {
	return func(hc *container.HostConfig) {
		hc.Memory = memoryB
	}
}

func networkPolicy(enable bool) policy {
	return func(hc *container.HostConfig) {
		if enable {
			hc.NetworkMode = network.NetworkDefault
		} else {
			hc.NetworkMode = network.NetworkNone
		}
	}
}

func restrictivePolicy(hc *container.HostConfig) {
	ulimitPolicy(&container.Ulimit{Name: "nproc", Hard: 1, Soft: 1})(hc)
	networkPolicy(false)(hc)
}
