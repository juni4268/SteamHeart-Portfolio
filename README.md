# SteamHeart

Unity 6로 만든 2D 사이드뷰 액션 어드벤처 **SteamHeart**의 게임 코드입니다.

- 영상과 구현 설명: https://juni4268.github.io/steamheart/
- 이 저장소는 포트폴리오용으로 **코드만** 공개합니다. 그래픽, 사운드, 씬, 프리팹은 팀원들의 작업물이라 포함하지 않았고, 이 저장소만으로는 Unity에서 게임을 실행할 수 없습니다.

## 개요

| | |
|---|---|
| 엔진 | Unity 6 (6000.3.11f1) · C# |
| 기간 | 2026년 8월~11월 (4개월) |
| 인원 | 5인 팀 '석복동' |
| 담당 | 게임 프로그래밍 전체, UI 제작, 레벨 구현·연출 적용 |

## 주요 구현 코드

소개 페이지에서 영상과 함께 설명하는 부분입니다.

| 내용 | 코드 |
|---|---|
| 최종 보스: 패턴이 쓸 부위(손 네 개와 링)를 예약 | [`Boss2PartUsage.cs`](Assets/Scripts/Boss/Boss2/Core/Boss2PartUsage.cs) |
| 최종 보스: 패턴의 실행과 종료·중단 처리 | [`Boss2PatternRunner.cs`](Assets/Scripts/Boss/Boss2/Core/Boss2PatternRunner.cs) |
| 중간 보스: 패턴 선택 | [`Boss1PatternSelector.cs`](Assets/Scripts/Boss/Boss1/Core/Boss1PatternSelector.cs) |
| 중간 보스: 낙하 잔해의 위치와 간격 | [`Boss1DebrisField.cs`](Assets/Scripts/Boss/Boss1/Patterns/Debris/Boss1DebrisField.cs) |
| 재도전: 한 번의 도전 상태와 영구 진행도 | [`PlayerRuntimeState.cs`](Assets/Scripts/Player/Progress/PlayerRuntimeState.cs), [`PlayerPermanentProgress.cs`](Assets/Scripts/Player/Progress/PlayerPermanentProgress.cs) |
| 재도전: 사망 후 재시작 | [`PlayerRespawnController.cs`](Assets/Scripts/Player/PlayerRespawnController.cs) |

## 그 외 코드

| 내용 | 위치 |
|---|---|
| 최종 보스의 주요 패턴 선택과 간격 | [`Boss2MainPatternScheduler.cs`](Assets/Scripts/Boss/Boss2/Core/Boss2MainPatternScheduler.cs) |
| 최종 보스의 패턴 여섯 가지 | [`Assets/Scripts/Boss/Boss2/Patterns`](Assets/Scripts/Boss/Boss2/Patterns) |
| 중간 보스의 패턴 순서와 잔해 재공급 | [`Boss1PatternScheduler.cs`](Assets/Scripts/Boss/Boss1/Core/Boss1PatternScheduler.cs) |
| 중간 보스의 패턴 네 가지 | [`Assets/Scripts/Boss/Boss1/Patterns`](Assets/Scripts/Boss/Boss1/Patterns) |
| 플레이어 공격(지상 2타, 공중 공격, 강타, 카운터) | [`Assets/Scripts/Player/Combat`](Assets/Scripts/Player/Combat) |
| 제작대 강화 수치 | [`UpgradeSettings.cs`](Assets/Scripts/Player/Progress/UpgradeSettings.cs) |
| 일반 적 | [`Assets/Scripts/Enemy`](Assets/Scripts/Enemy) |
| 함정, 포털, 컷신, 씬 전환 | [`Assets/Scripts/World`](Assets/Scripts/World) |
| 에디터 테스트 | [`Assets/Editor/Tests`](Assets/Editor/Tests) |

## 폴더

- `Assets/Scripts` — 게임 코드 (Boss, Player, Enemy, Combat, Weapon, World, UI, Camera)
- `Assets/Editor` — 에디터 도구와 테스트
- `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt` — 사용 패키지와 Unity 버전

## 팀

| 이름 | 역할 |
|---|---|
| 최준이 | 프로그래밍, 애니메이션·연출 적용, 맵 구성·레벨 구현, UI 제작·구현 |
| 전진범 | 총괄, 기획, 배경 디자인, 레벨 그래픽 디자인 |
| 남궁석 | 캐릭터·보스 디자인, 무기·이펙트 디자인, 사운드 디렉팅 |
| 임경섭 | 기획, 사운드 디자인 |
| 최지환 | 스토리보드, 편집 |
