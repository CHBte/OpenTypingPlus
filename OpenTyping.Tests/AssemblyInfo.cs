// WordCatalog.UseJsonForTest 처럼 프로그램 전체가 함께 쓰는 정적 상태를 바꾸는 테스트가 있어,
// 테스트끼리 동시에 돌지 않게 한다.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
