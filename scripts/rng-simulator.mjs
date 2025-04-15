const rngItem = (dropChance) => () => Math.random() < dropChance;

const baseball = rngItem(0.1);
const golfClub = rngItem(0.02);
const baton = rngItem(0.1);

const SAMPLES = 1_000_000;
const BUCKETS = 20;

const buckets = [...Array(BUCKETS)].map(() => 0);
let total = 0;

const individualChance = (n, p) => 1 - (1 - p) ** n;

const multiply = (numBaseball, numGolfClub, numBaton) => {
  return (
    individualChance(numBaseball, 0.1) *
    individualChance(numGolfClub, 0.02) *
    individualChance(numBaton, 0.1)
  );
};

const geometricMean = (numBaseball, numGolfClub, numBaton) => {
  return (
    (individualChance(numBaseball, 0.1) *
      individualChance(numGolfClub, 0.02) *
      individualChance(numBaton, 0.1)) **
    (1 / 3)
  );
};

const average = (numBaseball, numGolfClub, numBaton) => {
  return (
    (individualChance(numBaseball, 0.1) +
      individualChance(numGolfClub, 0.02) +
      individualChance(numBaton, 0.1)) /
    3
  );
};

function generateMappingTable(
  numSimulations,
  numBuckets,
  baseballProb = 0.1,
  golfClubProb = 0.02,
  batonProb = 0.1
) {
  // Function to calculate probability after n attempts
  const calcProbability = (n, p) => 1 - Math.pow(1 - p, n);

  // Function to calculate geometric mean of probabilities
  const geometricMean = (numBaseball, numGolfClub, numBaton) => {
    const baseballProb = calcProbability(numBaseball, 0.1);
    const golfClubProb = calcProbability(numGolfClub, 0.02);
    const batonProb = calcProbability(numBaton, 0.1);
    return Math.pow(baseballProb * golfClubProb * batonProb, 1 / 3);
  };

  // Function to simulate a run using geometric distribution
  function simulateRun() {
    // Generate random number of tries for each item using geometric distribution
    const baseballTries = Math.ceil(
      Math.log(Math.random()) / Math.log(1 - baseballProb)
    );
    const golfClubTries = Math.ceil(
      Math.log(Math.random()) / Math.log(1 - golfClubProb)
    );
    const batonTries = Math.ceil(
      Math.log(Math.random()) / Math.log(1 - batonProb)
    );

    // Calculate geometric mean
    return geometricMean(baseballTries, golfClubTries, batonTries);
  }

  // Run simulations and collect geometric mean values
  const results = [];
  for (let i = 0; i < numSimulations; i++) {
    results.push(simulateRun());
  }

  // Sort results to create percentile mapping
  results.sort((a, b) => a - b);

  // Create mapping table with "numBuckets" entries
  const mappings = [];
  for (let i = 0; i < numBuckets; i++) {
    const index = Math.floor((i / numBuckets) * results.length);
    mappings.push(results[index]);
  }

  // Add the maximum value to ensure the range goes up to 1.0
  // This helps prevent out-of-bounds errors when accessing the table
  mappings.push(1.0);

  return mappings;
}

// const mappings = generateMappingTable(100_000_000, 100);
// console.log(mappings.map((b) => +b.toFixed(4)).join(","));

const mappings = [
  0.0585, 0.1256, 0.1477, 0.1638, 0.1765, 0.188, 0.1974, 0.2066, 0.2145, 0.2229,
  0.2309, 0.2384, 0.2454, 0.252, 0.2581, 0.2642, 0.2704, 0.2766, 0.2827, 0.288,
  0.2937, 0.2989, 0.3045, 0.3097, 0.3147, 0.3198, 0.3249, 0.3297, 0.3349,
  0.3398, 0.3447, 0.3497, 0.3544, 0.3591, 0.364, 0.3686, 0.3731, 0.3779, 0.3826,
  0.3873, 0.3921, 0.3966, 0.4013, 0.4062, 0.4109, 0.4156, 0.4204, 0.4253,
  0.4301, 0.4351, 0.44, 0.4451, 0.4502, 0.4554, 0.4608, 0.4664, 0.4717, 0.477,
  0.4825, 0.4879, 0.4935, 0.4989, 0.5045, 0.51, 0.5155, 0.521, 0.5268, 0.5326,
  0.5384, 0.5443, 0.5502, 0.5564, 0.5625, 0.569, 0.5755, 0.5823, 0.5892, 0.5959,
  0.6029, 0.61, 0.6173, 0.6248, 0.6326, 0.6405, 0.6488, 0.6574, 0.6662, 0.6753,
  0.6849, 0.6948, 0.7055, 0.7168, 0.7287, 0.7416, 0.7557, 0.7711, 0.7887,
  0.8092, 0.8347, 0.8701, 1,
];
const percentileMapping = (numBaseball, numGolfClub, numBaton) => {
  const rawLuck = geometricMean(numBaseball, numGolfClub, numBaton);
  const mappingIndex = Math.floor(rawLuck * (mappings.length - 1));
  const lowerValue = mappings[mappingIndex];
  const upperValue = mappings[mappingIndex + 1];
  const lowerPercentile = mappingIndex / (mappings.length - 1);
  const upperPercentile = (mappingIndex + 1) / (mappings.length - 1);
  const ratio = (rawLuck - lowerValue) / (upperValue - lowerValue);
  return lowerPercentile + ratio * (upperPercentile - lowerPercentile);
};

const baseballOnly = (numBaseball, numGolfClub, numBaton) => {
  return individualChance(numBaseball, 0.1);
};

const golfClubOnly = (numBaseball, numGolfClub, numBaton) => {
  return individualChance(numGolfClub, 0.02);
};

const implementation = percentileMapping;

for (let i = 0; i < SAMPLES; i++) {
  let numBaseball = 0;
  let numGolfClub = 0;
  let numBaton = 0;
  do {
    numBaseball++;
  } while (!baseball());
  do {
    numGolfClub++;
  } while (!golfClub());
  do {
    numBaton++;
  } while (!baton());

  const score = implementation(numBaseball, numGolfClub, numBaton);
  const bucketIndex = Math.floor(score * BUCKETS);
  if (bucketIndex >= 0 && bucketIndex < BUCKETS) {
    total++;
    buckets[bucketIndex]++;
  }
}

console.log(` === ${implementation.name} ===`);
console.log(
  buckets
    .map(
      (b, i) =>
        `<${Math.round((i + 1) * (100 / BUCKETS))
          .toString()
          .padStart(3, " ")}% = ${Math.round((b / total) * 100)
          .toString()
          .padStart(3, " ")}% ${"█".repeat(Math.round((b / total) * 100))}`
    )
    .join("\n")
);

// console.log(buckets.map((b) => b / total).join(","));
