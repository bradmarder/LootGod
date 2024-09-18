import { useEffect, useState } from 'react';
import { flushSync } from 'react-dom';
import { Container, Row, Col, Button, Alert } from 'react-bootstrap';
import axios from 'axios';
import LootRequests from './lootRequests';
import CreateLootRequest from './createLootRequest';
import CreateLoot from './createLoot';
import Loots from './loots';
import GrantedLoots from './grantedLoots';
import ArchivedLoot from './archivedLoot';
import RaidAttendance from './raidAttendance';
import Upload from './upload';
import LinkAlt from './linkAlt';
import LeaderModule from './leaderModule';
import CreateGuild from './createGuild';
import NewLoot from './newLoot';
import { setAxiosInterceptors } from './utils';
import 'bootstrap/dist/css/bootstrap.min.css'
import 'sweetalert2/dist/sweetalert2.css'

const params = new URLSearchParams(window.location.search);
const key = params.get('key') ?? localStorage.getItem('key') ?? '';
const hasKey = key.length > 0;
if (hasKey) {
	localStorage.setItem('key', key);
	axios.defaults.headers.common['Player-Key'] = key;
}
axios.defaults.baseURL = 'api/';
setAxiosInterceptors();

// aborted requests are caught and throw null rejections, catch and ignore them
window.onunhandledrejection = e => e.reason == null && e.preventDefault();

export default function App() {
	const [raidNight, setRaidNight] = useState<boolean | null>(null);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState(false);
	const [isAdmin, setIsAdmin] = useState(false);
	const [isLeader, setIsLeader] = useState(false);
	const [lootLock, setLootLock] = useState(false);
	const [requests, setRequests] = useState<ILootRequest[]>([]);
	const [loots, setLoots] = useState<ILoot[]>([]);
	const [items, setItems] = useState<IItem[]>([]);
	const [attendanceCacheKey, setAttendanceCacheKey] = useState(0);
	const [linkedAltsCacheKey, setLinkedAltsCacheKey] = useState(0);
	const [intro, setIntro] = useState(!hasKey);
	const [messageOfTheDay, setMessageOfTheDay] = useState('');

	const refreshCache = () => setAttendanceCacheKey(x => x + 1);
	const refreshLinkedAltsCacheKey = () => setLinkedAltsCacheKey(x => x + 1);
	const getAdminStatus = (signal: AbortSignal) => {
		axios
			.get<boolean>('/GetAdminStatus', { signal })
			.then(x => setIsAdmin(x.data));
	};
	const getLeaderStatus = (signal: AbortSignal) => {
		axios
			.get<boolean>('/GetLeaderStatus', { signal })
			.then(x => setIsLeader(x.data));
	};
	const getLoots = (signal: AbortSignal) => {
		axios
			.get<ILoot[]>('/GetLoots', { signal })
			.then(x => setLoots(x.data));
	};
	const getItems = (signal: AbortSignal) => {
		axios
			.get<IItem[]>('/GetItems', { signal })
			.then(x => setItems(x.data));
	};
	const getLootRequests = (signal: AbortSignal) => {
		axios
			.get<ILootRequest[]>('/GetLootRequests', { signal })
			.then(x => setRequests(x.data));
	};
	const getLootLock = (signal: AbortSignal) => {
		axios
			.get<boolean>('/GetLootLock', { signal })
			.then(x => setLootLock(x.data));
	};
	const getMessageOfTheDay = (signal: AbortSignal) => {
		axios
			.get<string>('/GetMessageOfTheDay', { signal })
			.then(x => setMessageOfTheDay(x.data));
	};
	const lockLoot = (enable: boolean) => {
		setLoading(true);
		axios
			.post('/ToggleLootLock', { enable })
			.finally(() => setLoading(false));
	};
	const transitionRaidNight = (raid: boolean) => {
		if (!(document as any).startViewTransition) {
			return setRaidNight(raid);
		}
		(document as any).startViewTransition(() => {
			flushSync(() => setRaidNight(raid));
			return new Promise(resolve => setTimeout(resolve, 50));
		});
	};
	const createGuildCallback = () => {
		setIsAdmin(true);
		setIntro(false);
	};

	useEffect(() => {
		if (intro) { return; }

		const ac = new AbortController();

		getAdminStatus(ac.signal);
		getLoots(ac.signal);
		getLootLock(ac.signal);
		getLootRequests(ac.signal);
		getLeaderStatus(ac.signal);
		getItems(ac.signal);
		getMessageOfTheDay(ac.signal);
		
		return () => ac.abort();
	}, [intro]);

	useEffect(() => {
		if (intro) { return; }

		const es = new EventSource('/api/SSE?playerKey=' + localStorage.getItem('key'));
		es.addEventListener('lock', e => setLootLock(e.data === '[true]'));
		es.addEventListener('motd', e => setMessageOfTheDay(e.data));
		es.addEventListener('loots', e => setLoots(JSON.parse(e.data)));
		es.addEventListener('items', e => setItems(JSON.parse(e.data)));
		es.addEventListener('requests', e => setRequests(JSON.parse(e.data)));
		es.onopen = () => console.log('SSE connection established');
		es.onerror = () => { setError(true); es.close(); }

		return () => es.close();
	}, [intro]);

	return (
		<Container fluid>
			{error &&
				<Row>
				<Col xs={12} xl={6}>
				<Alert variant={'danger'}>
					<p>Disconnected from the server, please refresh the page</p>
				</Alert>
				</Col>
				</Row>
			}
			{!error && intro &&
				<Row>
				<Col xs={12} xl={6}>
				<CreateGuild finish={createGuildCallback}></CreateGuild>
				</Col>
				</Row>
			}
			{!error && !intro && raidNight != null &&
				<h1>{raidNight ? 'Raid' : 'Rot'} Loot</h1>
			}
			{!error && !intro && messageOfTheDay &&
				<Row>
				<Col xs={12} xl={6}>
				<Alert variant='warning'>
					<h5>MOTD</h5>
					{messageOfTheDay}
				</Alert>
				</Col>
				</Row>
			}
			{!error && !intro && raidNight == null &&
				<>
					<Row>
					<Col xs={12} xl={6}>
					<Alert variant={'primary'}>
						<p>Show me <strong>RAID NIGHT</strong> loot. What does this mean? If you are in an active raid and loot is dropping, this is the button you want to click.
						Click the wrong button or change your mind? No worries! This isn't permanent, simply refresh to page to select again.</p>
						<p>(ADMINS!) Click this to create/update quantities and grant requests for <strong>RAID NIGHT</strong> loots only.</p>
						<Button variant={'success'} onClick={() => transitionRaidNight(true)}>Show Raid Night Loot</Button>
					</Alert>
					</Col>
					</Row>

					<Row>
					<Col xs={12} xl={6}>
					<Alert variant={'danger'}>
						<p>Show me <strong>ROT</strong> loot. This allows you to request rot loot that nobody wanted during a previous raid. You can request this loot for your alts or main.
						Click the wrong button or change your mind? No worries! This isn't permanent, simply refresh to page to select again.</p>
						<p>(ADMINS!) Click this to create/update quantities or grant requests for <strong>ROT</strong> loots only.</p>
						<Button variant={'info'} onClick={() => transitionRaidNight(false)}>Show ROT Loot</Button>
					</Alert>
					</Col>
					</Row>
				</>
			}
			{!error && raidNight != null &&
				<Row>
					<Col xs={12} xl={6}>
						<CreateLootRequest requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock} raidNight={raidNight} linkedAltsCacheKey={linkedAltsCacheKey} items={items}></CreateLootRequest>
						<br />
						<LootRequests requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock} raidNight={raidNight} items={items}></LootRequests>
						<br />
						<LinkAlt refreshLinkedAltsCacheKey={refreshLinkedAltsCacheKey}></LinkAlt>
						<br />
						{isAdmin &&
							<GrantedLoots requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock} raidNight={raidNight} items={items}></GrantedLoots>
						}
						{isAdmin &&
							<ArchivedLoot requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock} raidNight={raidNight} items={items}></ArchivedLoot>
						}
					</Col>
					<Col xs={12} xl={6}>
						{isAdmin &&
							<>
								<Upload refreshCache={refreshCache}></Upload>
								<NewLoot></NewLoot>
								<CreateLoot items={items} raidNight={raidNight}></CreateLoot>
								{lootLock &&
									<Button variant={'success'} onClick={() => lockLoot(false)} disabled={loading}>Unlock/Enable Loot Requests</Button>
								}
								{!lootLock &&
									<Button variant={'danger'} onClick={() => lockLoot(true)} disabled={loading}>Lock/Disable Loot Requests</Button>
								}
								<br /><br />
								{isLeader &&
									<LeaderModule></LeaderModule>
								}
							</>
						}
						<Loots requests={requests} loots={loots} isAdmin={isAdmin} spell={true} raidNight={raidNight} items={items}></Loots>
						<br />
						<Loots requests={requests} loots={loots} isAdmin={isAdmin} spell={false} raidNight={raidNight} items={items}></Loots>
						<br />
						<RaidAttendance isAdmin={isAdmin} cacheKey={attendanceCacheKey}></RaidAttendance>
					</Col>
				</Row>
			}
		</Container>
	);
}