import { useEffect, useState } from 'react';
import './App.css';
import { Container, Row, Col, Button, Alert } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { LootRequests } from './lootRequests';
import { CreateLootRequest } from './createLootRequest';
import { CreateLoot } from './createLoot';
import Loots from './loots';
import { GrantedLoots } from './grantedLoots';
import { ArchivedLoot } from './archivedLoot';
import { RaidAttendance } from './raidAttendance';
import { Upload } from './upload';

const params = new Proxy(new URLSearchParams(window.location.search), {
	get: (searchParams, prop) => searchParams.get(prop as string),
});
const key = (params as any).key || localStorage.getItem('key');
if (key == null || key === '') {
	alert('Must have a player key. Ask your guild for one.');
	throw new Error();
}
localStorage.setItem('key', key);
axios.defaults.headers.common['Player-Key'] = key;

export default function App() {
	const [raidNight, setRaidNight] = useState<boolean | null>(null);
	const [loading, setLoading] = useState(false);
	const [isAdmin, setIsAdmin] = useState(false);
	const [lootLock, setLootLock] = useState(false);
	const [requests, setRequests] = useState<ILootRequest[]>([]);
	const [loots, setLoots] = useState<ILoot[]>([]);

	const getAdminStatus = async (signal: AbortSignal) => {
		const res = await axios.get<boolean>('/GetAdminStatus', { signal });
		setIsAdmin(res.data);
	};
	const getLoots = async (signal: AbortSignal) => {
		const res = await axios.get<ILoot[]>('/GetLoots', { signal });
		setLoots(res.data);
	};
	const getLootRequests = async (signal: AbortSignal) => {
		const res = await axios.get<ILootRequest[]>('/GetLootRequests', { signal });
		setRequests(res.data);
	};
	const getLootLock = async (signal: AbortSignal) => {
		const res = await axios.get<boolean>('/GetLootLock', { signal });
		setLootLock(res.data);
	};
	const enableLootLock = async () => {
		setLoading(true);
		try {
			await axios.post('/ToggleLootLock?enable=true');
		}
		finally {
			setLoading(false);
		}
	};
	const disableLootLock = async () => {
		setLoading(true);
		try {
			await axios.post('/ToggleLootLock?enable=false');
		}
		finally {
			setLoading(false);
		}
	};

	useEffect(() => {
		const controller = new AbortController();

		getAdminStatus(controller.signal);
		getLoots(controller.signal);
		getLootLock(controller.signal);
		getLootRequests(controller.signal);

		return () => controller.abort();
	}, []);

	useEffect(() => {
		const connection = new HubConnectionBuilder()
			.withUrl('/lootHub?key=' + key)
			.configureLogging(2) // signalR.LogLevel.Information
			.withAutomaticReconnect()
			.build();

		connection.on("lock", (lootLock: boolean) => {
			setLootLock(lootLock);
		});
		connection.on("loots", (loots: ILoot[]) => {
			setLoots(loots);
		});
		connection.on("requests", (requests: ILootRequest[]) => {
			setRequests(requests);
		});
		connection.onclose(error => {
			alert('Connection to the server has dropped. Can you reload the page please? Thank you.');
		});

		connection.start();

		return () => { connection.stop(); }
	}, []);

	return (
		<Container fluid>
			{raidNight != null &&
				<>
				<h1>{raidNight ? 'Raid' : 'Rot'} Loot</h1>
				<h3>Refresh page to switch to raid/rot loots</h3>
				</>
			}
			{raidNight == null &&
				<>
					<Row>
					<Col xs={12} xl={6}>
					<Alert variant={'primary'}>
						<p>Show me <strong>RAID NIGHT</strong> loot. What does this mean? If you are in an active raid and fresh and hot loot is dropping, this is the button you want to click.
						Click the wrong button or change your mind? No worries! This isn't permanent, simply refresh to page to select again.</p>
						<p>(ADMINS!) Click this to create/update quantities and grant requests for <strong>RAID NIGHT</strong> loots only.</p>
						<Button variant={'success'} onClick={() => setRaidNight(true)}>Show Raid Night Loot</Button>
					</Alert>
					</Col>
					</Row>

					<Row>
					<Col xs={12} xl={6}>
					<Alert variant={'danger'}>
						<p>Show me <strong>ROT</strong> loot. This allows you to request junky rot loot that nobody wanted during a previous raid. You can request this loot for your alts or main.
						Click the wrong button or change your mind? No worries! This isn't permanent, simply refresh to page to select again.</p>
						<p>(ADMINS!) Click this to create/update quantities or grant requests for <strong>ROT</strong> loots only.</p>
						<Button variant={'info'} onClick={() => setRaidNight(false)}>Show ROT Loot</Button>
					</Alert>
					</Col>
					</Row>
				</>
			}
			{raidNight != null &&
				<Row>
					<Col xs={12} xl={6}>
						<CreateLootRequest requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock} raidNight={raidNight}></CreateLootRequest>
						<br />
						<LootRequests requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock} raidNight={raidNight}></LootRequests>
						<br />
						{isAdmin &&
							<GrantedLoots requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock} raidNight={raidNight}></GrantedLoots>
						}
						{isAdmin &&
							<ArchivedLoot requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock} raidNight={raidNight}></ArchivedLoot>
						}
					</Col>
					<Col xs={12} xl={6}>
						{isAdmin &&
							<>
								<Upload></Upload>
								<CreateLoot loots={loots} raidNight={raidNight}></CreateLoot>
								{lootLock &&
									<Button variant={'success'} onClick={disableLootLock} disabled={loading}>Unlock/Enable Loot Requests</Button>
								}
								{!lootLock &&
									<Button variant={'danger'} onClick={enableLootLock} disabled={loading}>Lock/Disable Loot Requests</Button>
								}
								<br /><br />
							</>
						}
						<Loots requests={requests} loots={loots} isAdmin={isAdmin} spell={true} raidNight={raidNight}></Loots>
						<br />
						<Loots requests={requests} loots={loots} isAdmin={isAdmin} spell={false} raidNight={raidNight}></Loots>
						<br />
						<RaidAttendance isAdmin={isAdmin}></RaidAttendance>
					</Col>
				</Row>
			}
		</Container>
	);
}