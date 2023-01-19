import { useEffect, useState } from 'react';
import './App.css';
import { Container, Row, Col, Button } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import { HubConnectionBuilder } from '@microsoft/signalr';
//import { Login } from './login';
import { LootRequests } from './lootRequests';
import { CreateLootRequest } from './createLootRequest';
import { CreateLoot } from './createLoot';
import Loots from './loots';
import { GrantedLoots } from './grantedLoots';
import { ArchivedLoot } from './archivedLoot';
import { RaidAttendance } from './raidAttendance';
import { Upload } from './upload';

const api = process.env.REACT_APP_API_PATH;

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
	const [loading, setLoading] = useState(false);
	const [isAdmin, setIsAdmin] = useState(false);
	const [lootLock, setLootLock] = useState(false);
	const [requests, setRequests] = useState<ILootRequest[]>([]);
	const [loots, setLoots] = useState<ILoot[]>([]);

	const getAdminStatus = async (signal: AbortSignal) => {
		const res = await axios.get<boolean>(api + '/GetAdminStatus', { signal });
		setIsAdmin(res.data);
	};
	const getLoots = async (signal: AbortSignal) => {
		const res = await axios.get<ILoot[]>(api + '/GetLoots', { signal });
		setLoots(res.data);
	};
	const getLootRequests = async (signal: AbortSignal) => {
		const res = await axios.get<ILootRequest[]>(api + '/GetLootRequests', { signal });
		setRequests(res.data);
	};
	const getLootLock = async (signal: AbortSignal) => {
		const res = await axios.get<boolean>(api + '/GetLootLock', { signal });
		setLootLock(res.data);
	};
	const enableLootLock = async () => {
		setLoading(true);
		try {
			await axios.post(api + '/ToggleLootLock?enable=true');
		}
		finally {
			setLoading(false);
		}
	};
	const disableLootLock = async () => {
		setLoading(true);
		try {
			await axios.post(api + '/ToggleLootLock?enable=false');
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
			.withUrl(api + "/lootHub?key=" + key)
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

	// const logout = () => {
	// 	localStorage.removeItem('name');
	// 	setMainName('');
	// 	setIsReady(false);
	// };
	// const finishLogin = (name: string, admin: boolean) => {
	// 	setMainName(name);
	// 	setIsAdmin(admin);
	// 	setIsReady(true);
	// };

	return (
		<Container fluid>
			<h1>{process.env.REACT_APP_TITLE}</h1>
			{/* {!isReady &&
				<Login finishLogin={finishLogin} />
			} */}
			{true &&
				<Row>
					<Col xs={12} xl={6}>
						<CreateLootRequest requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock}></CreateLootRequest>
						<br />
						<LootRequests requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock}></LootRequests>
						<br />
						{isAdmin &&
							<GrantedLoots requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock}></GrantedLoots>
						}
						{isAdmin &&
							<ArchivedLoot requests={requests} loots={loots} isAdmin={isAdmin} lootLocked={lootLock}></ArchivedLoot>
						}
					</Col>
					<Col xs={12} xl={6}>
						{/* <Alert variant='secondary'>
							Logged in as <strong>{mainName}</strong>
							<br />
							<Button onClick={logout}>Logout</Button>
						</Alert> */}
						{isAdmin &&
							<>
								<Upload></Upload>
								<CreateLoot loots={loots}></CreateLoot>
								{lootLock &&
									<Button variant={'success'} onClick={disableLootLock} disabled={loading}>Unlock/Enable Loot Requests</Button>
								}
								{!lootLock &&
									<Button variant={'danger'} onClick={enableLootLock} disabled={loading}>Lock/Disable Loot Requests</Button>
								}
								<br /><br />
							</>
						}
						<Loots requests={requests} loots={loots} isAdmin={isAdmin} spell={true}></Loots>
						<br />
						<Loots requests={requests} loots={loots} isAdmin={isAdmin} spell={false}></Loots>
						<br />
						<RaidAttendance isAdmin={isAdmin}></RaidAttendance>
					</Col>
				</Row>
			}
		</Container>
	);
}