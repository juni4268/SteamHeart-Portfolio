# SteamHeart

> 이 저장소는 포트폴리오용으로 **코드만** 공개합니다. 그래픽, 사운드, 씬, 프리팹은 팀원들의 작업물이라 포함하지 않았기 때문에, 이 저장소만으로는 Unity에서 게임을 실행할 수 없습니다.

위아래로 나뉜 스팀펑크 도시를 오르는 2D 사이드뷰 액션 어드벤처입니다. 5인 팀 '석복동'이 Unity 6로 만들었습니다.

쓰러진 할아버지를 살리기 위해 코어를 찾아 나서는 이야기입니다. 주인공은 어두운 하층 마을에서 출발해 고철 처리장과 수직 통로를 거쳐 상층 도시까지 올라갑니다.

## 게임 특징

- **반복 도전**: 사망하면 처리장 입구로 돌아오지만, 모아 둔 고철과 영구 강화는 유지됩니다. 제작대에서 더 강화한 뒤 다시 출발합니다.
- **고정된 맵과 스토리**: 맵과 보스가 매번 바뀌지 않고, 카툰풍 컷신 6편으로 이야기가 진행됩니다.
- **자체 제작 도트**: 주인공, 적, 보스, NPC의 도트 그림과 동작을 모두 직접 만들었습니다.

## 개요

| | |
|---|---|
| 장르 | 2D 사이드뷰 액션 어드벤처 |
| 엔진 | Unity 6 (6000.3.11f1) |
| 조작 | 키보드 · 게임패드 |
| 구성 | 씬 8개 · 보스 2종 · 일반 적 3종 · 함정 2종 · 컷신 6편 |
| 소개 페이지 | https://juni4268.github.io/steamheart/ |

## 프로그래밍 담당

이 저장소의 게임 프로그래밍 전체와 UI 제작, 레벨 구현·연출 적용을 최준이가 맡았습니다.

## 주요 구현 코드

| 내용 | 코드 |
|---|---|
| 최종 보스의 부위 예약 | [`Boss2PartUsage.cs`](Assets/Scripts/Boss/Boss2/Core/Boss2PartUsage.cs) |
| 최종 보스 패턴의 실행과 종료 처리 | [`Boss2PatternRunner.cs`](Assets/Scripts/Boss/Boss2/Core/Boss2PatternRunner.cs) |
| 보스 1의 패턴 선택 | [`Boss1PatternSelector.cs`](Assets/Scripts/Boss/Boss1/Core/Boss1PatternSelector.cs) |
| 보스 1의 낙하 잔해 | [`Boss1DebrisField.cs`](Assets/Scripts/Boss/Boss1/Patterns/Debris/Boss1DebrisField.cs) |
| 도전 상태와 영구 진행도 | [`PlayerRuntimeState.cs`](Assets/Scripts/Player/Progress/PlayerRuntimeState.cs), [`PlayerPermanentProgress.cs`](Assets/Scripts/Player/Progress/PlayerPermanentProgress.cs) |
| 사망 후 재시작 | [`PlayerRespawnController.cs`](Assets/Scripts/Player/PlayerRespawnController.cs) |

각 항목의 설명과 영상은 소개 페이지의 구현 방식에 있습니다.

## 폴더

- `Assets/Scripts` — 게임 코드 (Boss, Player, World 등)
- `Assets/Editor` — 에디터 도구와 테스트 (`Assets/Editor/Tests`)
- `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt` — 사용 패키지와 Unity 버전

## 팀

| 이름 | 역할 |
|---|---|
| 최준이 | 프로그래밍, 애니메이션·연출 적용, 맵 구성·레벨 구현, UI 제작·구현 |
| 전진범 | 총괄, 기획, 배경 디자인, 레벨 그래픽 디자인 |
| 남궁석 | 캐릭터·보스 디자인, 무기·이펙트 디자인, 사운드 디렉팅 |
| 임경섭 | 기획, 사운드 디자인 |
| 최지환 | 스토리보드, 편집 |
