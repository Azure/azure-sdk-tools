using NUnit.Framework;

// Enable parallel execution for all tests in the assembly
[assembly: Parallelizable(ParallelScope.All)]

// Set conservative parallelism
[assembly: LevelOfParallelism(10)]