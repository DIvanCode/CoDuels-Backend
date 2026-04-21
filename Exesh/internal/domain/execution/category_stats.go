package execution

type CategoryStats struct {
	TimeSamplesByCategory   map[string]int
	MedianTimeByCategory    map[string]int
	MaxTimeByCategory       map[string]int
	MemorySamplesByCategory map[string]int
	MedianMemoryByCategory  map[string]int
	MaxMemoryByCategory     map[string]int
}

func NewCategoryStats() CategoryStats {
	return CategoryStats{
		TimeSamplesByCategory:   make(map[string]int),
		MedianTimeByCategory:    make(map[string]int),
		MaxTimeByCategory:       make(map[string]int),
		MemorySamplesByCategory: make(map[string]int),
		MedianMemoryByCategory:  make(map[string]int),
		MaxMemoryByCategory:     make(map[string]int),
	}
}
