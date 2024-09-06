import { useState, useEffect } from 'react';
import { Alert, Button, Form } from 'react-bootstrap';
import axios from 'axios';

export default function leaderModule() {

	const [loading, setLoading] = useState(true);
	const [raidDiscord, setRaidDiscord] = useState('');
	const [rotDiscord, setRotDiscord] = useState('');
	const [transferName, setTransferName] = useState('');
	const [discordSuccess, setDiscordSuccess] = useState(false);
	const [transferSuccess, setTransferSuccess] = useState(false);
	const [messageOfTheDay, setMessageOfTheDay] = useState('');

	const transferLeadership = () => {
		setLoading(true);
		axios
			.post('/TransferGuildLeadership?name=' + transferName)
			.then(_ => setTransferSuccess(true))
			.finally(() => setLoading(false));
	};
	const updateDiscord = async () => {
		setLoading(true);
		setDiscordSuccess(false);
		const a = axios.post('/GuildDiscord?raidNight=true&webhook=' + encodeURIComponent(raidDiscord));
		const b = axios.post('/GuildDiscord?raidNight=false&webhook=' + encodeURIComponent(rotDiscord));
		Promise
			.all([a, b])
			.then(() => setDiscordSuccess(true))
			.finally(() => setLoading(false));
	};
	const getDiscord = () => {
		axios
			.get<{ raid: string, rot: string }>('/GetDiscordWebhooks')
			.then(x => {
				setRaidDiscord(x.data.raid);
				setRotDiscord(x.data.rot);
			})
			.finally(() => setLoading(false));
	};
	const getMessageOfTheDay = () => {
		axios
			.get<string>('/GetMessageOfTheDay')
			.then(x => setMessageOfTheDay(x.data));
	};
	const uploadMessageOfTheDay = () => {
		setLoading(true);
		axios
			.post('/UploadMessageOfTheDay?motd=' + encodeURIComponent(messageOfTheDay))
			.finally(() => setLoading(false));
	};
	useEffect(getDiscord, []);
	useEffect(getMessageOfTheDay, []);

	return (
		<Alert variant='dark'>
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Guild Message of the Day</Form.Label>
					<Form.Control as="textarea" rows={3} placeholder='Enter guild MOTD' value={messageOfTheDay} onChange={e => setMessageOfTheDay(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='primary' disabled={loading} onClick={uploadMessageOfTheDay}>Update</Button>
			</Form>
			<hr />
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Transfer Guild Leadership</Form.Label>
					<Form.Control type="text" placeholder='Enter new guild leader name' value={transferName} onChange={e => setTransferName(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='warning' disabled={loading || transferName.length < 4} onClick={transferLeadership}>Transfer</Button>
				{transferSuccess &&
					<Alert variant='success'>Successfully transferred leadership</Alert>
				}
			</Form>
			<hr />
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Raid Discord Webhook URL</Form.Label>
					<Form.Control type="text" placeholder='Enter Raid Discord webhook URL' value={raidDiscord} onChange={e => setRaidDiscord(e.target.value)} />
				</Form.Group>
				<hr />
				<Form.Group>
					<Form.Label>Rot Discord Webhook URL</Form.Label>
					<Form.Control type="text" placeholder='Enter Rot Discord webhook URL' value={rotDiscord} onChange={e => setRotDiscord(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='warning' disabled={loading} onClick={updateDiscord}>Update</Button>
				{discordSuccess &&
					<Alert variant='success'>Successfully updated Discord webhook URL</Alert>
				}
			</Form>
		</Alert>
	);
}